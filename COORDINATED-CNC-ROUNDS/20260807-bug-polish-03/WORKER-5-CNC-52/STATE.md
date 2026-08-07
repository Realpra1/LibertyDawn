# Worker State: CNC-52

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `5`
- Task: `CNC-52 — Starting-Fact wall hole prevention/repair`
- Change category: `AI base-construction policy/behavior bug fix with persisted bounded state`
- Balance authority: `Frozen. Do not change costs, HP, armor, damage, speed, build time, power, prerequisites, probabilities, resource values, wall caps, or other balance/tuning values.`
- Status: `Implementation and evidence loop`
- Common base branch/SHA: `agent/cnc-20260807-bug-polish-02-release` / `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
- Task branch: `agent/round-20260807-cnc52-first-fact-wall-holes`
- Intended PR base: `agent/cnc-20260807-bug-polish-03-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `20`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/COORDINATED-CNC-ROUNDS/20260807-bug-polish-03/WORKER-5-CNC-52/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-5-cnc52`
- Persistent policy scratchpad: `/root/github/LibertyDawn/.agents/references/LIBERTY-DAWN-POLICY-SCRATCHPAD.md` (3,000
  characters maximum; one cross-round serialized writer)
- Policy scratchpad lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/shared-locks`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `33` (`control-game-03`, `control-game-04`, `changed-game-01`, exact-base `control-game-05`, changed `changed-game-02`, cycle-2 fresh-save `changed-game-03`, valid reload `changed-game-05`, cycle-3 diagnostic `changed-game-06`, cycle-4 queue-owner `changed-game-07`, strict cycle-4 pass `changed-game-08`, cycle-5 pending-owner fresh `changed-game-09`, exact reload `changed-game-10`, cycle-6 cadence reload `changed-game-11`, cycle-7 telemetry reload `changed-game-12`, cycle-8 controlled-map fresh `changed-game-14`, cycle-9 deterministic fresh/reload `changed-game-15`/`16`, cycle-10 exact-cutoff reload `changed-game-17`, cycle-11 post-review reload `changed-game-18`, cycle-12 fixed-geometry contention `changed-game-19`, cycle-13 original-Fact-loss fresh/reload `changed-game-20`/`21`, cycle-14 low-cash final-stress diagnostic `changed-game-22`, cycle-15 raid-path diagnostic `changed-game-23`, cycle-16 final changed/exact-base pair `changed-game-24`/`control-game-06`, cycle-17 simultaneous diagnostic pair `changed-game-25`/`control-game-07`, cycle-18 stock-default simultaneous pair `changed-game-26`/`control-game-08`, cycle-19 stock-default serial pair `control-game-09`/`changed-game-27`, and cycle-20 connected-map natural-conclusion `changed-game-28`; cycle-13 fresh and cycles 14/15/17 are counted as materially judged setup/stress failures and tick-0 harness failures are excluded)
- Terra cycle code reviews: `cycle-05 concern adopted: bound inactive defense-queue polling to enclosure cadence; cycle-10 concern adopted for cycle 11: when generic placement is randomly ordered, deterministically search the full annulus for a legal unreserved alternative before overriding, without consuming additional RNG`
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

The existing first-Fact enclosure is all-or-nothing. One blocked or illegal
perimeter cell prevents every segment, each failed planning pass consumes one of
eight attempts, and the Fact is then permanently abandoned. A completed Fact is
also permanently marked handled, so a later destroyed segment is never repaired.
The ordinary building placer has no knowledge of planned enclosure cells. This
matches the preserved observation of Brutalis 2 abandoning its starting
enclosure after eight blocked attempts.

Change only this bounded opening behavior. During the first 7,500 world ticks
(five minutes at the repository's default 40 ms timestep), the first Fact
deployed from the starting MCV should retain a stable, access-preserving plan;
ordinary friendly construction should avoid consuming its wall cells when a
comparably sensible legal site exists; legal missing cells should be built or
repaired independently when they become available. At tick 7,500 the policy
must stop and release its reservations. Later Facts must never be enclosed.

## Authoritative behavior

- Bind once, deterministically, to the first owned configured construction-yard
  actor observed from the starting deployment. Persist that identity/plan across
  save/load. If it dies, undeploys, changes owner, or otherwise ceases to be that
  Fact, do not transfer the policy to another Fact.
- Retain a stable perimeter derived from that Fact and its configured margin,
  with at least one deliberate access opening that is not classified as a hole.
  Prove the access using actual two-way Harvester and ground-combat traffic.
- Before tick 7,500, regard other planned perimeter cells as soft reservations.
  Ordinary/discretionary construction uses another legal site when one has no
  material safety, timing, travel, exit, or build-radius penalty. Critical power,
  refinery, production, and recovery work may override only when no comparably
  sensible alternative exists; an allowed occupying building is never sold or
  moved merely to make the ring prettier.
- Recheck the small retained perimeter on a bounded periodic cadence. Build
  independently legal missing runs/cells; keep transiently occupied or
  temporarily illegal cells pending; repair destroyed eligible wall cells if
  they become legal before the cutoff. Tolerate fixed terrain, map-edge, and
  build-radius impossibilities without blocking other legal segments or
  thrashing production.
- At and after tick 7,500, issue no new enclosure request, reservation, placement,
  or repair. Do not cancel already-issued construction. Damage or newly free
  cells after the cutoff cannot reactivate it.
- Preserve existing wall type availability, segment caps, queue/cash rules,
  deterministic ordering, normal AI modules, and save/replay synchronization.
  Put tunable policy in the owning BaseBuilder rules/config and the small
  algorithmic/state invariant in cohesive wall/enclosure code.

## Forbidden behavior and failure signals

- No sealed or functionally unusable enclosure, trapped Harvester/army, blocked
  production exit/rally lane, or access claim supported only by a path query or
  wall count.
- No all-or-nothing legality gate, fixed-attempt abandonment, completed-once
  terminal flag, or corner-only repair that misses an interior destroyed cell.
- No selection or reselection of a second/later Fact, including after the first
  Fact is lost, and no maintenance at/after tick 7,500.
- No cosmetic wall priority that materially delays or worsens power, refinery,
  factory, opening, or emergency recovery; no selling/moving a valid critical
  building to reclaim a cell.
- No per-tick/full-map scan, unbounded retries/allocations, duplicate wall orders,
  stale queue ownership, nondeterministic set iteration, or persistent noisy log
  spam. A request/diagnostic without final placed/repaired wall and traffic is
  not a pass.
- No changes to general tower-wall self-blocking/selling (CNC-46), Tiberium field
  walls (CNC-41 ownership), unrelated AI strategy/personality, non-CNC mods, or
  frozen balance.

## Relevant current implementation and control behavior

At base `468ee64f...`, `BaseBuilderWallPlanner` owns tower walls and starting-Fact
enclosures. `TryPlanConstructionYardEnclosure` chooses the lowest-ActorID live
Fact, computes a 16-cell perimeter for the CNC 3x3 Fact at margin 1, demands that
every missing cell pass `CanAnchorAt`, and queues only missing corners as
`LineBuild` anchors. `MaxEnclosureAttempts = 8`; failures wait the existing
500-tick retry delay, then add the Fact to `handledEnclosureYards`. Completion
also adds it permanently. A missing non-corner segment after completion therefore
cannot be repaired. `pendingAnchors` and enclosure identity/handled state are not
saved.

`BaseBuilderQueueManager.ChooseBuildingToBuild` gives a producible enclosure wall
priority after low-power and serialized missing-refinery recovery, then places it
through `TakeWallCell`; ordinary `ChooseBuildLocation` and special first-tower,
economy-SAM, and Tiberium-field placements do not consult enclosure cells. The
planner uses `BuildingInfluence`, `world.CanPlaceBuilding`, base-radius legality,
the configured wall list/cap, and deterministic geometry. `BaseBuilderBotModule`
already owns game-save trait data and is the appropriate serialization boundary.
All CNC AI profiles enable enclosure wall types; Brutalis also owns smart economy
and Tiberium-field policy. Walls use 1x1 `LineBuild` actors with range 15; current
unit tests cover geometry/perimeter shape but not lifecycle, reservations,
cutoff, partial repair, or save/load.

## Likely wrong approaches and challenges

- Raising or removing the eight-attempt limit while retaining the whole-ring
  legality test; it merely delays the same abandonment and can create churn.
- Treating the lowest ActorID on every scan as “starting Fact,” or rebinding after
  loss/load; this walls expansion Facts and violates the literal scope.
- Closing all 16 cells, choosing a doorway without vehicle-sized traffic proof,
  refilling the intentional access cell, or moving the perimeter when blocked.
- Protecting cells with a global hard exclusion that strands critical
  construction, or bypassing only generic placement while special planners can
  still consume cells without an explicit priority decision.
- Repairing only corners/anchors, considering an existing wall actor sufficient
  without ownership/type/legal-state checks, or consuming stale pending anchors
  after another queue/actor changes the world.
- Replanning every tick, scanning all buildings/map cells, exact graph solving,
  rigid partitions, or elaborate reservation machinery. The retained perimeter
  is tiny; use a deterministic local rule/cooldown and bounded diagnostics.
- Unit tests, passive fixtures, activation logs, reloaded games, or repeated
  happy-path runs as sole proof. Do not tune wall caps/cost/build duration or any
  other balance value to manufacture improvement.

## Competing systems and ownership

- `BaseBuilderQueueManager` owns structure selection, production, cash use, and
  final placement. Its low-power, smart-economy missing-refinery, opening,
  production, refinery, silo, and generic fraction paths compete for Fact queues
  and cash; placement arbitration must preserve their existing priority.
- `BaseBuilderFirstTowerPlanner` and economy-SAM placement choose dedicated sites;
  force them to contend in an integrated test. They must not silently consume a
  reserved cell or be displaced into a materially worse site.
- `BaseBuilderTiberiumFieldManager` can request/place the same `brik`/`sbag` actors
  and owns field-wall reservations. Keep its actor/queue/placement ownership
  distinct and test simultaneous demand without duplicate/stolen anchors.
- General `BaseBuilderWallPlanner` tower walls (especially Skynet), CNC-46's
  self-blocking/selling work, `MaximumWallSegments`, `LineBuild`, building
  influence, build-radius checks, terrain, units, and other queue actors can all
  invalidate or consume a wall plan. Diagnose request, rejection reason,
  reservation owner, competing consumer, placement order, and final actor.
- MCV manager/opening/smart-economy can create later MCVs/Facts; these are hard
  negative controls. Building repair/auto-heal may repair damaged surviving wall
  actors, but rebuilding a destroyed hole remains enclosure ownership.
- Save/load and replay must preserve identity/cutoff deterministically and must
  not generate new bot orders during replay or a desync.

## Cross-worker dependencies

CNC-46 is the direct same-round overlap. It owns general wall self-blocking and
selling; CNC-52 owns only starting-Fact identity, its access-preserving planned
cells, construction avoidance, and incomplete-cell repair before tick 7,500.
The CNC-46 branch/PR was not present at spec time. Ask the coordinator only for
its branch/PR identifier when available, then inspect that PR's commits (never its
state/spec) at implementation start and before publication. If it changes
`BaseBuilderWallPlanner.cs`, `BaseBuilderQueueManager.cs`, wall reservations, or
selling, preserve its general behavior and route a semantic conflict to the
coordinator/integrator; do not absorb or rewrite CNC-46.

CNC-41 owns red-tree/Resonator field-wall behavior and is excluded prior-round
work. Do not change its policy; preserve `BaseBuilderTiberiumFieldManager`
interfaces. No task prerequisite changes the recorded base SHA. Likely cohesion
pressure in the already mixed wall planner favors a focused starting-enclosure
helper/state boundary if needed, but avoid unrelated refactoring.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-5-cnc52/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-5-cnc52/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `mostly sensible; medium confidence`
- Recommendations adopted as testable hypotheses: `A deliberately open partial ring is useful only with real two-way Harvester and combat traffic; soft reservations yield to critical construction without a comparable safe/timely site; compare eligible-cell completion and cleared/damaged-cell recovery against control; bound wall order volume/MAX cost; hard-negative-test later Facts and the tick-7,500 cutoff; test enemy pressure through the opening.`
- Recommendations rejected or deferred, with reason: `No in-scope recommendation rejected. Whole-match wins remain secondary to the local behavior but are retained as regression evidence; no balance or general wall-policy expansion is authorized.`
- Persistent scratchpad update: `Validated 1,707-character replacement atomically promoted to the canonical scratchpad; added a provisional access-proven, soft-reservation, bounded early-enclosure rule.`

## Acceptance and tests

### Literal black-box acceptance

Create a deterministic task-local CNC enclosure/hole `.oramap` fixture that starts
the tested ordinary AI with an MCV (not a preselected later Fact), keeps all
normal AI modules active, and records exact map checksum, seed, factions, bots,
starts, options, actors, and scripted perturbation ticks. A non-access planned
cell is blocked through the control's eighth retry and becomes legal before tick
7,500; another eligible wall cell is destroyed after initial placement; a second
MCV deploys a second Fact before the cutoff; the intentional opening is exercised
by ordinary two-way Harvester and combat traffic.

In a fresh changed-AI run, the first Fact retains a stable plan, builds all
currently legal non-access segments without waiting for impossible/occupied
cells, fills the cleared cell and rebuilds the destroyed eligible cell before
tick 7,500, keeps the deliberate opening usable, and never targets the second
Fact. Destroy/free another cell at or after tick 7,500 and run beyond tick 9,000:
no new enclosure reservation/order/repair occurs. Critical power, refinery, and
production progress remains materially equivalent to the matched control. Logs
must link identity, eligibility/rejection, reservation/override, request, order,
placement/repair, access, and cutoff to the final visible actors/outcome.

### Focused checks and instrumentation

- Before cycle 1, capture base-SHA focused geometry tests and a short control run.
  Add unit/interface tests for stable perimeter/access exclusion, partial legal
  runs, transient versus fixed blockage, interior-hole repair candidates,
  deterministic ordering, first-Fact/later-Fact/cutoff decisions, and save-data
  round-trip/backward-absent fields. Unit tests supplement games only.
- Build shared engine/CNC and run the focused NUnit fixture plus the repository's
  CNC-applicable lint/static/unit suite; run the broad shared test suite before
  publication. Do not build/test/package other mods except unavoidable shared
  engine compilation.
- Use bounded opt-in diagnostics (custom-map rule override may enable existing
  enclosure logging): target Fact ID/location, fixed cutoff/access cells,
  candidate status (`wall`, `intentional-access`, `occupied` with actor/owner,
  terrain/map/build-radius illegal), reservation owner, priority override,
  queue/type/cash request, stale invalidation, issued `LineBuild`, resulting wall
  actor, repair, and stop reason. Warnings/errors must be actionable; never
  silently treat rejection as success. Remove temporary/per-tick logs before PR.
- Confirm save data is deterministic, version/backward tolerant, and does not
  serialize transient world objects incorrectly. Confirm no unauthorized balance,
  CNC-46/CNC-41, non-CNC, raw artifact, or unrelated worktree changes.
- Measure wall scans/orders/allocations and matched MAX benchmark throughput.
  The hot path must inspect only the retained small perimeter on a cooldown, do
  no work after cutoff, and show no credible regression (investigate >5% matched
  throughput loss; repeat to distinguish noise).

### Ordinary and differential games

Cycle 1's first behavioral evidence must be a matched two-game base-control versus
changed pair on the blocked-then-cleared custom fixture, full CNC engine, ordinary
Brutalis (including normal economy/opening/field modules) and ordinary opponent,
headless MAX, same map bytes/seed/factions/starts/options/actors. The obvious
blocker may be cheese, but no passive/custom bot or manager-only harness is
allowed. Prove MAX activation, intended bots/map/options, tick progress past
9,000, logs/replay/benchmark flush, and final actor outcomes. A same-build toggle
is optional; otherwise use isolated base SHA `468ee64f...` as control.

Then climb difficulty rather than repeat the smoke: (1) open ordinary start with
natural construction contention; (2) temporary unit/building blocker plus
independent wall damage; (3) cliff/water/map-edge/build-radius enclosure with
some permanently impossible cells; (4) urgent low-power/refinery recovery and
special-planner/Tiberium-wall contention; (5) second Fact, first-Fact loss, and
damage immediately before/after cutoff; (6) cramped asymmetric access under
enemy pressure. Run matched seeds when judging policy. Run at least one ordinary
connected-map full match from MCV deployment to natural conclusion at headless
MAX, with enclosure behavior occurring naturally. Routing is not the feature, so
Archipelago is required only if used to provide the hostile blocked topology; a
purpose-built cliff/water enclosure fixture is preferred.

Save a fresh changed run before a pending clear/repair and before the cutoff,
reload it, and prove the same first-Fact identity, access plan, pending cell, and
absolute cutoff survive; then repeat the key result from a fresh non-reloaded
game. Play back a fresh accepted replay through the relevant ticks and require no
desync, fatal exception, or divergent recorded outcome. Saves/replays are not
sole acceptance. Use the shared resource wrapper and isolated supports/artifacts.

### Old-behavior control and required improvement

Golden control is exact SHA `468ee64f5a0f9a9e19e260e5c5943e6e878f4705`
in an isolated worktree. Record content/config checksums. Keep map, seed,
factions, starts, bots, teams, lobby options, initial actors/resources, scripted
ticks, and duration identical. Base is expected to log eight blocked attempts,
abandon the Fact, leave the cleared/destroyed cells missing, and never recover.

Primary changed-versus-control metrics: eligible non-access planned cells filled
over time; legal segments built despite other impossible cells; cleared transient
cell filled; destroyed eligible cell restored before cutoff; latency from
clear/damage to final wall; successful two-way Harvester/combat passages; zero
cells/orders around later Facts; zero new work at/after tick 7,500. Changed must
decisively beat control on exercised completion/recovery, not merely emit logs.

Guardrails: wall requests/orders/spend, futile retry count, queue/cash idle time,
first power/refinery/factory milestones and site quality, Fact survival, useful
damage/losses, match outcome, and MAX throughput/allocation evidence. No critical
milestone may be skipped or materially worsened for cosmetic completion. A loss,
parity/marginal local gain, trapped traffic, repeated futile orders, or >5% repeatable
MAX slowdown requires diagnosis/correction or a concrete task-specific account;
feature activation is never enough.

### Adversarial cases

After the latest relevant fix, obtain at least these three distinct clean
full-engine ordinary-AI scenarios (restart the three-clean count after a fix):

1. **Dynamic hole/access pressure.** Failure hypothesis: retained cells are
   dropped, corner-only repair misses an interior hole, or the doorway traps
   vehicles/defenders. Perturbation: clear a blocker after the eighth control
   attempt, destroy an interior wall, and attack through the intended opening.
   Failure: missing eligible cell at cutoff, refill of access, stalled traffic,
   or Fact exposed worse than control. Pass: both walls restored before cutoff,
   access remains open, real Harvester/combat traffic crosses both ways, and the
   AI responds normally.
2. **Geometry and construction contention.** Failure hypothesis: one impossible
   cell vetoes all legal segments, reservations force unsafe/late critical
   construction, or field/first-tower/SAM planners steal ownership. Perturbation:
   cliff/water/map/build-radius holes plus an overlapping opening structure, once
   with a comparable alternate site and once without, with Tiberium-wall demand.
   Failure: queue thrash/all abandonment/duplicate anchor, materially worse
   critical site/timing, or sale of valid structure. Pass: legal useful segments
   finish, impossible cells are quiet, alternate is used only when sensible, and
   ownership/critical progress is intact.
3. **Identity, persistence, and cutoff.** Failure hypothesis: load/reselection
   targets a later Fact or repair continues after expiry. Perturbation: second
   Fact before cutoff, save/load with pending hole, first-Fact loss/undeploy, and
   damage immediately on both sides of tick 7,500. Failure: any later-Fact wall,
   identity/gate change, work after cutoff, or replay desync. Pass: only the
   original Fact is considered, pre-cutoff state resumes, post-cutoff cells stay
   untouched, and replay is clean.

For every run record failure hypothesis, new perturbation, exact failure signal,
and player-visible pass evidence before launch. Unexpected behavior is explicitly
accepted or treated as defective; do not count unexercised paths.

### Final regression

From a fresh start after the final change, rerun the literal custom fixture with
all normal Brutalis and competing modules, stronger early enemy pressure, and the
matched base control. Require the changed run to fill the late-cleared and
destroyed eligible cells before tick 7,500, retain traffic-proven access, preserve
critical construction, ignore the second Fact, and perform no repair after the
cutoff through tick 9,000. Then run a fresh ordinary connected-map headless-MAX
match to natural conclusion. Validate save/load separately and replay the fresh
literal run with no desync/fatal errors. Run focused and broad applicable tests,
confirm required GitHub checks green, diagnostics cleaned, balance frozen, and
CNC-46/CNC-41 ownership preserved. A reload-only or log-only result fails.

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
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-03/analysis/worker-5-cnc52/cycle-review-05/CYCLE-REVIEW.md`.

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
| 1 | Uncommitted bounded first-Fact plan/lifecycle, per-cell runs and repair, soft placement reservations, persisted identity/plan, cutoff, focused policy tests | One blocked cell may still veto legal runs; queue ownership may miss an interior repair; policy may retarget the second Fact or work after cutoff. Literal fixture blocks `20,29` until tick 7000, destroys `18,31` at tick 6000, deploys a later Fact at tick 5200, and destroys `22,30` after cutoff. Corrected-map rerun (`3a220ab3…`) commands real Harvester/tank traffic onto the actual access cells and logs exact actor snapshots. | Exact-base `BotWallGeometryTest` passed; changed geometry/policy selection passed 19/19. Full-engine controls `03`/`04`, changed `01`, corrected exact-base control `05`, and corrected changed `02` reached tick 9000. Corrected matched throughput: control 374.626 ticks/s; changed about 345.808 ticks/s (7.7% lower; repeat required). | Narratives: `cycle-01-commenter/NARRATIVE.md`, `cycle-01-v2-commenter/NARRATIVE.md`. Reviews: first mostly sensible/medium; corrected pair unsound/high due failed traffic. Adopted stable obstruction/route evidence; rejected the proposed ban on refilling `20,29` because it is the north perimeter while tested traffic is south at `21,33–36`, so the causal corridor claim was unsupported. Both reviewers emitted no valid scratchpad replacement; canonical retained. CNC-46 had no implementation commit/PR. | Control `05` exhausted eight attempts and left all tested walls empty. Changed `02` visibly had `sbag` at the late blocker, repair, and pre-cutoff target by tick 7490; kept all access cells empty and later-Fact wall count zero; after destroying `22,30` at 7600 it remained empty at 8500. Harvester reached access at 2623; tank reached at 8692, but return did not finish by 9000, so two-way traffic acceptance failed. | Persist enclosure cells as integer bits (generic `CPos[]` save formatting is not round-trip safe), persist bounded pending/issued ownership, and add serialization tests. Correct the traffic activity harness, then repeat the matched performance pair. |
| 2 | Encode persisted plan cells as integer bits; persist/validate bounded pending anchors, observed walls, and issued-cell timestamps; fail closed on inconsistent state. | Save/load may corrupt comma-containing `CPos[]` geometry or lose queue ownership, causing duplicate placement, later-Fact rebind, missed repair, or post-cutoff work. Fresh fixture saved at tick 5900 before destruction/clear/cutoff, then reloaded the exact save. | Focused geometry/policy tests passed 21/21. Fresh-save game reached tick 10000 and wrote `cycle02-pending.orasav`; valid custom-map reload reached tick 10000 at 384.117 ticks/s. The first reload attempt failed at tick 0 because its external map was not staged and is excluded. | Narrative: `cycle-02-commenter/NARRATIVE.md`. Policy: `cycle-02-policy/POLICY-REVIEW.md`, mixed/high. Adopted its request for per-cell rejection and explicit return-route evidence; retained first-yard identity/cutoff. Its first two conclusions remained hypotheses pending causal diagnostics. No valid scratchpad replacement was emitted, so the canonical scratchpad was retained. | Reload restored exact `145/fact@19,30` at tick 5901 with the same access/scan state, resumed a destroyed-cell repair plan at 6434, never rebound the later Fact, and stopped at cutoff with no late work. Persistence passed. Scenario failed: repair placement became stale/illegal and was dropped; traffic return did not complete. | Add bounded per-cell rejection/stale-anchor diagnostics, fix the fixture's over-frequent movement cancellation, identify the actor/legality cause, then revise only if product evidence requires it. |
| 3 | Add opt-in bounded stale-anchor and per-cell legality diagnostics; correct the fixture's 250-tick movement retry cadence and explicit return-order/arrival telemetry. | The repair may be rejected by hidden placement state, and the declared access may fail real two-way traffic. The same literal map now records exact occupants/rejection categories and stable inbound/outbound events. | Focused geometry/policy tests passed 21/21. `changed-game-06-cycle03-diagnostics` reached tick 10000 in 22.018 seconds with no fatal/desync. Its wrapper label failed only because two snapshot regexes required an extra literal space; the emitted values satisfy both assertions and the ignored manifests are corrected. | Narrative: `cycle-03-commenter/NARRATIVE.md`. Policy: `cycle-03-policy/POLICY-REVIEW.md`, mostly sensible/medium. Adopted its corrected automated rerun and harder cash/pressure/contention recommendations; its latency concern remains a measurement target rather than a rule change. No valid scratchpad replacement was emitted, so the canonical scratchpad was retained. | Exact diagnostics showed only the intended neutral `barb#147` at blocked `20,29`. Destroyed interior `18,31` was planned 6094, issued 6214, confirmed 6627. Cleared `20,29` was issued 7144 and visible at 7490. Harvester and tank crossed inward and outward by 7159. Access stayed empty, later-Fact walls stayed zero, and the wall destroyed at 7600 stayed empty through 9800 after cutoff stop at 7608. | Product repair/access behavior is validated for the literal fixture. Address evidenced duplicate request/queue ownership risk and persist the exact owner before the next save/load adversary. CNC-46 still has no implementation commit/PR at cycle start. |
| 4 | Serialize each pending enclosure endpoint to one exact `ProductionQueue`; persist actor/type/reservation tick in version-3 save data, safely release version-2 pending anchors, and restore only a live matching queued wall. | A second live Fact queue may duplicate or steal the same endpoint, or stale ownership may survive save/cancel/cutoff. Literal run deploys the second Fact before repair and late clearance and logs each request queue. | Focused geometry/policy tests passed 23/23. `changed-game-07-cycle04-queue-ownership` reached tick 10000 but failed strict traffic at 7490 by 54 ticks. Corrected `changed-game-08-cycle04-strict-pass-pending-save` passed every automated assertion through tick 10000 at 499.347 ticks/s and wrote `cycle04-pending-owner.orasav`. | Failed-run narrative/policy: `cycle-04-commenter` and `cycle-04-policy`, mixed/medium; adopted strict earlier traffic. Passing-run narrative/policy: `cycle-04-v2-commenter` and `cycle-04-v2-policy`, mostly sensible/medium; adopted pending-through-cutoff and early-pressure/cash tests. Both emitted no valid scratchpad replacement, so canonical retained. | Every endpoint had exactly one request/issue owner. Queue `145/Defence.GDI` owned opening/repair cells; with a second Fact live, another queue singly owned a late endpoint while the target stayed yard `145@19,30`. Strict rerun traffic passed at 5694; repair confirmed 6598; late blocker was visible at 7490; access/later-Fact/cutoff snapshots passed. The tick-6125 save preceded its request at 6180, so pending-owner restore remains unexercised. | Cycle 5: reject out-of-range saved issue/reservation ticks, move save to 6200, and run fresh plus reload as the two materially useful games. Then perform the required cycle-5 Terra code review. |
| 5 | Reject negative/future persisted issue and queue-reservation timestamps before restoring ownership. | A corrupt/future timestamp may hold stale ownership indefinitely; a valid tick-6200 save must reconstruct the exact live queue and complete one repair without duplication. | Focused tests passed 27/27. Fresh `changed-game-09` passed all strict assertions at 499.559 ticks/s; save checksum `e67e54…` captured queue `270/Defence.GDI` reserved at 6180 before issue at 6302. Reload `changed-game-10` reached 10000, restored exact owner at 6201, issued once at 6295, and confirmed repair at 6563, but failed blocker snapshots. | Paired narrative: `cycle-05-commenter/NARRATIVE.md`. Policy: `cycle-05-policy/POLICY-REVIEW.md`, mixed/high; adopted bounded prompt rechecks, not unsupported wall priority over critical work. Terra: `cycle-review-05/CYCLE-REVIEW.md`, advisory concern at planner reconciliation; adopted via cadence clamp. Neither policy role emitted a valid scratchpad replacement; canonical retained. | Persistence/ownership passed: same yard/type/queue/reserved tick, no invalid-state/legacy/desync/fatal, one repair issue/confirmation, access pass, no later-Fact or post-cutoff work. Functional reload failed: blocker cleared 7000 but next request waited until 7445 and cutoff 7529 prevented placement, leaving `blocker=empty`. | Cycle 6 will cap only inactive defense-queue polling to the existing 250-tick enclosure maintenance interval while active. This preserves priority and eliminates the 445-tick recheck gap without per-tick work. |
| 6 | Cap only the defense queue's otherwise-inactive sleep by the existing enclosure maintenance interval while the first-yard policy is active. | The prior 445-tick delay may be queue-manager sleep; a 250-tick cap should request the cleared cell by 7250 without per-tick scanning or priority changes. | Focused tests passed 30/30. Exact reload `changed-game-11-cycle06-cadence-reload` reached tick 10000 with no fatal/desync/invalid restore but failed both blocker snapshots. | Narrative: `cycle-06-commenter/NARRATIVE.md`. Policy: `cycle-06-policy/POLICY-REVIEW.md`, mixed/medium; adopted explicit post-clear trace, retained strict cutoff and access distinction, and deferred old-control repetition until the changed path passes. No valid scratchpad replacement emitted; canonical retained. | Exact owner restored and repair confirmed at 6567. After blocker clearance at 7000, no enclosure plan/request occurred at all before exact cutoff stop at 7500. The clamp therefore did not address the active-production path; blocker stayed empty while access/later-Fact/post-cutoff behavior passed. | Do not raise wall priority or cancel production without evidence. Cycle 7 adds bounded opt-in queue telemetry (current item/done, readiness, cash/resources and poll time) to distinguish continuous legitimate production from planner scheduling. |
| 7 | Add opt-in, per-queue, 250-tick-capped enclosure contention telemetry; no queue priority, cancellation, or balance change. | The late cell may be starved by continuous legitimate defense production rather than planner sleep. Record current item, readiness, remaining time/cost, and resources to distinguish those cases. | Focused tests passed 30/30. Exact reload `changed-game-12-cycle07-queue-telemetry` passed every strict assertion through tick 10000 at 555.010 ticks/s. A later attempted reload after changing the external map package failed before tick 0 because the save pins the old map UID and is excluded. | Narrative: `cycle-07-commenter/NARRATIVE.md`. Policy: `cycle-07-policy/POLICY-REVIEW.md`, mostly sensible/medium; adopted matched queue/timing evidence and a persistent/near-cutoff blocker adversary, and rejected blanket wall priority over valuable defense. No valid scratchpad replacement emitted; canonical retained. | At reload tick 6202 queue 270 had the restored sandbag with 78 ticks remaining while queue 145 had an Obelisk with 318. Repair issued 6287 and confirmed 6548. Queue 145 legitimately started another Obelisk; after blocker clearance 7000, an idle queue became available at 7177, requested `20,29`, issued 7288, and the wall was visible at 7490. Access, later-Fact identity, and cutoff assertions all passed. | Queue contention is legitimate and the existing priority rule is retained. Cycle 8 uses a newly saved map with blocker clearance at 6500 to provide a controlled 1000-tick runway, then reloads it to exercise exact owner persistence under the same map UID. |
| 8 | Fixture-only perturbation: schedule blocker clearance at tick 6500 and create a new save under that exact map UID. | A full 1000-tick post-clearance runway should distinguish queue contention from cutoff starvation, then support a same-UID reload. | `changed-game-14-cycle08-controlled-fresh-save` passed all assertions through tick 10000 at 554.756 ticks/s and wrote save checksum `814cccd…`. The planned paired reload was not run because the repair was requested at tick 6201, after the tick-6200 save, so pending ownership was absent. | Narrative: `cycle-08-commenter/NARRATIVE.md`. Policy: `cycle-08-policy/POLICY-REVIEW.md`, mostly sensible/medium; it correctly rejected the contradictory blocker timing as recovery evidence, retained normal SAM/Obelisk priority, and treated late engineer captures as out of scope. No valid scratchpad replacement emitted; canonical retained. | Functional behavior passed: first Fact only, repair confirmed 6711, two-way traffic 5731, correct pre/final snapshots, cutoff 7500. The intended controlled timing failed: neutral `barb#147` disappeared during ordinary play, so `20,29` was planned 5189, issued 5288, and confirmed 5754 before the scripted `blocker cleared tick=6500` marker. | Cycle 9 makes the fixture blocker a friendly non-enclosure wall so ordinary AI cannot remove it early, verifies occupied status until scripted clearance, and moves the save to tick 6250 so the repair's exact queue owner is genuinely persisted before a same-UID reload. |
| 9 | Fixture-only determinism correction: friendly non-enclosure blocker held through tick 6400, scripted clear 6500, save 6250. | The planner must defer a proven live occupant, persist the exact pending repair owner, resume after load, and complete both repair and newly legal blocker cell before cutoff without touching the later Fact. | Fresh `changed-game-15` and reload `changed-game-16` both passed all assertions through tick 10000 at 554.801/554.624 ticks/s. Save checksum `3a14582c…`; exact pending owner `queue=250/Defence.GDI reserved-tick=6191`. | Paired narrative: `cycle-09-commenter/NARRATIVE.md`. Policy: `cycle-09-policy/POLICY-REVIEW.md`, mostly sensible/medium; retained deferred no-build for friendly occupancy, rejected a relocation subsystem and blanket wall priority, and accepted the 169-tick reload variance because all invariants completed before cutoff. No valid scratchpad replacement emitted; canonical retained. | Fresh/reload logged `barb#147/Multi0` occupying `20,29` before clearance, with no pre-clear issue. Reload restored yard 145 and exact queue owner at 6252, issued repair 6315, confirmed 6952, issued newly legal `20,29` at 7067, and confirmed 7413. Both snapshots proved two-way access, no later-Fact walls, and no post-cutoff replacement. Stop/release was observed at 7502 fresh and 7524 reload because lifecycle cleanup currently waits for the next planner call. | Cycle 10 adds an exact per-bot-tick cutoff lifecycle hook that only releases pending ownership/reservations at tick 7500; it must not cancel already-issued construction or change queue priority. Add a focused exact-tick test, rerun the deterministic reload, then perform the required cycle-10 Terra code review. |
| 10 | Invoke enclosure lifecycle maintenance from each base-builder bot tick so cutoff cleanup occurs exactly at tick 7500; add exact-boundary test. | Lazy cleanup can leave internal soft reservations/queue ownership marked for several ticks after the literal cutoff even when no late order is issued. Per-bot-tick maintenance should release only planner ownership at 7500 without cancelling an already-issued wall. | Focused tests passed 31/31; unrelated baseline CA1825 warning remains in `AircraftHuskSpawnEligibilityTest.cs:23`. `changed-game-17-cycle10-exact-cutoff-reload` passed all assertions through tick 10000 at 525.415 ticks/s. | Narrative: `cycle-10-commenter/NARRATIVE.md`. Policy: `cycle-10-policy/POLICY-REVIEW.md`, mostly sensible/medium; it explicitly endorsed exact cutoff, retained normal queue arbitration, and recommended ordinary pressure/second-yard tests. No valid scratchpad replacement emitted; canonical retained. Terra: `cycle-review-10/CYCLE-REVIEW.md`, one advisory concern adopted. | Reload restored exact queue 250/reservation 6191, issued repair 6303 and blocker cell 6880, showed all three walls at 7490, then logged `tick=7500 stopped ... reservations released`. The wall destroyed 7600 stayed empty at 9800; access, traffic, later-Fact, save-state, and no-late-work assertions passed. Reviewer found the randomized generic-placement sample can override after eight reserved candidates despite a later equal alternative. | Cycle 11 adopts the concern narrowly: preserve the normal shuffled first-choice path and RNG use, but if its bounded sample contains only reserved legal sites, use a deterministic full-annulus fallback scan for any legal unreserved site before overriding. Then restart the three-clean-scenario acceptance count. |
| 11 | Adopt cycle-10 review: after a randomized generic-placement sample finds only reserved legal cells, deterministically scan the same full annulus for a legal unreserved alternative before override; preserve normal shuffle/RNG path. | Eight random legal candidates are not evidence that no equally sensible unreserved site exists. The fallback must avoid enclosure cells whenever any legal site remains, without changing RNG consumption for ordinary placements. | Focused tests passed 32/32, including a ninth-candidate unreserved fallback; unrelated baseline CA1825 warning remains. Acceptance scenario 1 `changed-game-18-cycle11-post-review-reload` passed all assertions through tick 10000 at 587.262 ticks/s. | Narrative: `cycle-11-commenter/NARRATIVE.md`. Policy: `cycle-11-policy/POLICY-REVIEW.md`, mostly sensible/medium; retained normal SAM/Obelisk arbitration and exact cutoff, and called for opening, pressured breach, and construction-contention evidence. No valid scratchpad replacement emitted; canonical retained. | Post-final-fix reload restored queue 250/reservation 6191, issued repair 6317 and confirmed 6797, held blocker through 6400, issued it only after clear at 6908 and confirmed 7169, preserved two-way traffic/access/later-Fact exclusion, and released at exactly 7500. No fatal/desync/invalid restore/late work. | Clean adversarial acceptance count is 1/3: dynamic occupied hole, interior repair, save/load, traffic and cutoff. Cycle 12 creates a distinct fixed-obstruction/construction-contention opening with ordinary generic placement plus first-tower, economy-SAM, and Tiberium-field planners enabled and observed. |
| 12 | Fixture-only fixed-geometry/contention opening: two permanent friendly non-enclosure blockers, enabled planner diagnostics, economy-II prerequisite, ordinary full Brutalis modules. | Legal segments and a destroyed interior cell must progress despite fixed occupied cells; first tower/SAM/Tiberium-field and normal defense construction must contend without consuming reserved cells or blocking access. | Acceptance scenario 2 `changed-game-19-cycle12-fixed-geometry-contention` passed all assertions through tick 10000 at 554.751 ticks/s. | Narrative: `cycle-12-commenter/NARRATIVE.md`. Policy: `cycle-12-policy/POLICY-REVIEW.md`, mostly sensible/medium. It endorsed geometry/access/repair/cutoff, identified the unrelated Tiberium extension stall for separate work, and proposed permanent blocker exclusion. That proposal is rejected: the contract requires potentially transient occupancy to remain pending, no placement request was issued on either blocker, and the bounded 13-cell/250-tick scan showed no production pressure. No valid scratchpad replacement emitted; canonical retained. | Both `barb` blockers remained live through 9800; zero issues targeted `20,29`/`18,32`. Legal walls built, `18,31` was destroyed 6000, issued 6269, confirmed 6730. First tower chose `20,28` instead of blocked preferred `20,29`; economy-SAM reserved/placed at multiple anchors; Tiberium-field planning contended for but did not obtain its extension queue. Traffic, later-Fact exclusion, exact cutoff, and final gap assertions passed. | Clean adversarial acceptance count is 2/3. Cycle 13 uses ordinary cash plus scripted hostile pressure, saves with a pre-cutoff repair pending, kills the original Fact while a later Fact is live, reloads, and proves no identity transfer, stale ownership, or post-loss/cutoff work. |
| 13 | Fixture-only identity/persistence/loss adversary: ordinary cash and modules, hostile infantry pressure, later Fact, save at tick 6250 with repair queue ownership pending, original Fact loss at tick 6500. | Save/load or first-Fact loss may duplicate/stale the repair, transfer identity to the live later Fact, close access, or permit new work after loss/cutoff. | Fresh `changed-game-20-cycle13-loss-fresh-save` reached tick 10000 and produced save checksum `5db2a8b6…`; its wrapper failed only because three stale fixture assertions expected the later Fact at `39,30` instead of the consistently observed `38,29`. Corrected exact reload `changed-game-21-cycle13-loss-reload` passed every assertion through tick 10000 at 665.738 ticks/s. | Paired narrative: `cycle-13-commenter/NARRATIVE.md`. Policy: `cycle-13-policy/POLICY-REVIEW.md`, mostly sensible/medium. It endorsed persisted original-Fact identity, independent repair, traffic access, and stop-on-loss. Its bounded repair-priority suggestion is rejected for product change: the evidence shows zero cash and active defensive production, and does not establish a safe reason to displace survival/economy arbitration. No valid scratchpad replacement emitted; canonical retained. | Fresh/reload both planned repair `18,31` at 6085. Reload restored exact `queue=145/Defence.GDI reserved-tick=6085`, issued once at 6396, and preserved the wall. Hostile pressure acted at 5600; two-way Harvester/tank traffic passed. Original Fact `145@19,30` died at 6500 while later Fact `38,29` lived; the planner stopped and released ownership immediately, never rebound, and snapshots at 7490/9800 showed access empty, later-Fact walls zero, and the tick-7600 destroyed wall still empty. | Clean adversarial acceptance count is 3/3 after the latest product fix. The fresh-coordinate mismatch is accepted as a corrected harness oracle because the fresh and reload evidence agree exactly on `38,29`; proceed to final literal differential, replay, natural match, matched performance, broad checks, dependency reinspection, and publication gates without changing product behavior. |
| 14 | Fixture-only final-regression stress: literal blocked/cleared/damaged/later-Fact scenario plus four hostile infantry, with an extra reduction from the literal fixture's 100000 cash to 20000. | Under stronger pressure and low cash, the planner may monopolize critical work, close access, miss the cutoff, or reveal that the final two walls are infeasible under ordinary queue contention. | `changed-game-22-final-literal` reached tick 10000 with exit 0 and no fatal/desync, but correctly failed strict snapshots: `20,29` and repaired `18,31` were empty at 7490/9800. | Narrative: `cycle-14-commenter/NARRATIVE.md`. Policy: `cycle-14-policy/POLICY-REVIEW.md`, insufficient evidence/high. It correctly refused acceptance and requested post-clear constraint telemetry plus a matched control. Its suggestion that the evidence cannot distinguish scheduling from inability is rejected: existing bounded queue telemetry shows both Fact defense queues continuously building Obelisks with cash at zero from before damage/clear through cutoff. No valid scratchpad replacement emitted; canonical retained. | Access traffic passed at 5350, later-Fact walls stayed zero, and cutoff stopped exactly at 7500. The wall policy made no illegal or late request. At every observed opportunity after damage/clear, queue 145 or 258 held an unfinished Obelisk and cash was zero; remaining Obelisk cost was still 940/990 at tick 7452/7393. The missing walls therefore reflect the deliberately added low-cash/continuous-critical-defense constraint, not abandonment or lost reconciliation. | Do not change wall priority or balance: the contract explicitly preserves queue/cash rules and forbids cosmetic wall work from delaying critical defenses. Restore the literal fixture's recorded 100000 cash, keep the new hostile-pressure dimension, and run the exact-base/current matched final differential with unchanged strict wall/access/cutoff assertions. |
| 15 | Fixture-only final-regression coupling diagnostic: restored literal 100000 cash while retaining four hostile infantry spawned south of the first Fact on the scripted traffic route. | The stressed fixture must complete both walls and two-way traffic; failure could expose enclosure blockage or accidental interference between the new raid and the route witness. | `changed-game-23-final-literal` reached tick 10000 with exit 0/no fatal/desync. Wall/snapshot behavior passed, but strict traffic failed: both units reached the access cells and received outbound orders before the four hostile infantry spawned directly along their southern route; traffic remained false. | Narrative: `cycle-15-commenter/NARRATIVE.md`. Policy: `cycle-15-policy/POLICY-REVIEW.md`, mixed/medium. It endorsed the wall recovery/cutoff result and recommended diagnosing the fixture route before any policy change. No valid scratchpad replacement emitted; canonical retained. This was fixture-only, so the product-change cycle-15 Terra gate did not occur. | Repair `18,31` issued 6255/confirmed 6779. Cleared `20,29` issued 6903/confirmed 7359. Access cells stayed empty, later-Fact walls stayed zero, post-cutoff damage stayed unrepaired, and exact cutoff passed. Traffic Harvester/tank entered at 4823/5281 and were ordered south at 5281; raiders spawned at y=34 on that same corridor at 5400, after which the witness Harvester failed the exact return. | Treat this as invalid stress coupling, not an enclosure defect. Move the same four-unit raid to the north side of the first Fact so it still pressures the completed/damaged ring without intersecting the deliberate southern traffic witness; then run the strict exact-base/current pair. |
| 16 | Corrected final literal differential: identical `literal.oramap` checksum `439d95c8…`, seed 520052, bots/factions/spawns/options/100000 cash, four-infantry north-side raid, exact base `468ee64f…` versus current. | Current behavior must decisively recover the cleared/destroyed cells without blocking traffic, retargeting the later Fact, or issuing after cutoff; exact base should reproduce eight-attempt abandonment. | Current `changed-game-24-final-literal` and exact-base `control-game-06-final-literal` both passed all strict assertions through tick 10000. Current throughput 293.482 ticks/s versus control 383.386 is contaminated by dissimilar concurrent repository load and requires a two-slot matched repeat. Focused enclosure tests passed 20/20; `make check` passed with zero warnings/errors; `make test` built CNC/shared Release and passed all CNC MiniYAML checks. | Paired narrative: `cycle-16-commenter/NARRATIVE.md`. Policy: `cycle-16-policy/POLICY-REVIEW.md`, mostly sensible/medium. It judged the local current behavior genuinely better than control, endorsed the bounded policy, and retained urgent survival construction over walls. Its additional-route recommendation is already covered by the three clean adversarial scenarios and natural-match gate. No valid scratchpad replacement emitted; canonical retained. | Current repaired `18,31` (planned 6065/issued 6160/confirmed 6769), filled cleared `20,29` (planned 6769/issued 6878/confirmed 7291), passed two-way traffic at 4897, kept later-Fact walls zero, stopped exactly 7500, and left tick-7600 damage empty. Exact base logged attempts 1–8, gave up the first Fact, left all exercised cells empty, and then began enclosure attempts on the later Fact. Both maintained ordinary harvesting and survived the north-side raid setup without fatal/desync. | Literal acceptance and final behavioral differential pass. Next reserve both game slots for a simultaneous performance repeat, replay this accepted fresh run, complete the ordinary connected-map natural match, broad tests, report/dependency/publication/final-review gates. |
| 17 | Two-slot simultaneous literal performance diagnostic using the same final map/manifests as cycle 16. | A matched-load repeat should distinguish prior environmental noise from credible overhead without regressing behavior. | Exact base `control-game-07-final-performance` passed at 399.237 ticks/s. Current `changed-game-25-final-performance` reached tick 10000 and completed both exercised walls/cutoff, but its traffic witness did not finish under this run, so the batch correctly failed. Raw current duration 29.046s (~344.281 ticks/s), 13.8% below control. | Narrative: `cycle-17-commenter/NARRATIVE.md`. Policy: `cycle-17-policy/POLICY-REVIEW.md`, unsound/high for this individual failed traffic run. It correctly refuses to accept this run as traffic proof; its proposed runtime route-validation product change is rejected because the immediately prior accepted fresh run on the identical map/seed proved the same complete wall topology supports two-way traffic, three other clean scenarios proved traffic, and this test's purpose/setup was performance with heavy opt-in diagnostics. No valid scratchpad replacement emitted; canonical retained. | Wall behavior remained correct: repair issued 6323/confirmed 6722; blocker issued 6818/confirmed 7302; zero late/later-Fact work. Traffic timing varied: tank inbound only at 5450 and the fixture's ordinary bot-owned witnesses were redirected before exact return. The map enables current-only bounded queue/enclosure diagnostics throughout the hot interval, while exact base emits only eight attempt messages, so this is not a matched default-configuration performance measure. | Retain accepted fresh traffic evidence and no product change. Run a simultaneous stock Twin Lakes pair with the default `ConstructionYardEnclosureDebugLogging=false`, identical tick cap/map/seed/bots/options, to measure production-default overhead; then use a separate natural-conclusion Twin Lakes run with debug only to prove natural enclosure behavior. |
| 18 | Production-default performance pair: stock Twin Lakes checksum `192b69e7…`, seed 520252, 20000 cash, identical Brutalis/Easiest bots and options, current versus exact base simultaneously under an exclusive two-slot reservation; enclosure diagnostics disabled. | If the earlier slowdown was only opt-in logging/environment noise, production-default throughput should fall within 5% under matched load. | Both games passed tick 10000/no fatal/desync. Current `changed-game-26-stock-performance` ran at 453.922 ticks/s; exact base `control-game-08-stock-performance` at 554.652 ticks/s, an 18.2% wall-clock difference. Broad `dotnet test` passed 533/533; only unrelated pre-existing CA1825 at `AircraftHuskSpawnEligibilityTest.cs:23`. | Narrative: `cycle-18-commenter/NARRATIVE.md`. Policy: `cycle-18-policy/POLICY-REVIEW.md`, insufficient evidence/high, appropriately refuses performance acceptance without isolation. It asks for content-identified profiling/repeat; focused behavioral evidence already exists in cycles 11–16. No valid scratchpad replacement emitted; canonical retained. | Both stock simulations were clean and maintained harvesting. Benchmark averages show current `tick_time` 1.1055ms versus control 1.0622ms (+4.1%, inside threshold), but current emitted 401268 benchmark samples versus control 236521 because the two simultaneously competing processes experienced unequal local-frame/wait-loop counts; total wall-clock throughput therefore cannot yet be attributed to enclosure CPU cost. | Reserve both slots but run exact control and current serially inside that reservation, eliminating process scheduling and third-party load. If the serial repeat still exceeds 5%, optimize the bounded enclosure hot path and rerun affected acceptance; otherwise retain the current bounded implementation with the per-sample evidence. |
| 19 | Fixture-only production-default serial performance repeat: the exact same stock Twin Lakes manifests/checksum, seed, bots, cash, options, and diagnostics-disabled content, with exact base and current run one after the other inside one exclusive two-slot reservation. | If current enclosure code causes the simultaneous pair's 18.2% wall-clock loss, a serial repeat isolated from peer simulation and third-party game load should reproduce a >5% loss in the same direction. | Exact base `control-game-09-stock-performance-serial` passed at 369.900 ticks/s; current `changed-game-27-stock-performance-serial` passed at 434.247 ticks/s, reversing direction with current 17.4% faster. Both reached tick 10000 without fatal/desync. | Narrative: `cycle-19-commenter/NARRATIVE.md`. Policy: `cycle-19-policy/POLICY-REVIEW.md`, insufficient evidence/high for task behavior, correctly avoids treating unrelated harvest/extension observations as enclosure proof. No valid scratchpad replacement emitted; canonical retained. | The wall-clock direction reversal disproves a repeatable >5% enclosure slowdown in these host conditions. The comparable per-sample `tick_time` evidence from cycle 18 was +4.1%, inside threshold. Unequal local-frame/benchmark sample counts explain why short total-duration comparisons are scheduling/startup sensitive. | Accept performance: bounded 13-cell scans run only on cadence, diagnostics default off, and no credible repeatable regression remains. Keep the long-lived resource-extension stall out of CNC-52 scope. Run the required natural connected-map match with task diagnostics enabled. |
| 20 | Fixture-only natural full-match gate: stock connected Twin Lakes with only opt-in enclosure diagnostics overridden, seed 520152, 20000 cash, ordinary Brutalis GDI spawn 1 versus Easiest Nod spawn 5, headless MAX, no tick cap. | The bounded policy might work only in scripted fixtures, interfere with a normal opening, fail to finish naturally, or retain reservations beyond cutoff during a long ordinary match. | `changed-game-28-natural-twin-lakes` passed, exited 0 at natural game over after progress through tick 45000 (debug through 46610), and had no fatal/desync/invalid-state message. It bound first yard 356 at tick 12, confirmed all independently needed sandbag cells by tick 6117, and released reservations exactly at tick 7500. Final focused tests passed 32/32. | Narrative: `cycle-20-commenter/NARRATIVE.md`. Policy: `cycle-20-policy/POLICY-REVIEW.md`, insufficient evidence/high as expected for a natural run without forced hole/control; it nevertheless endorses the bounded plan and exact release. Its request for constructed blocked-cell/repair/control proof is already satisfied by cycles 11–16. The first-request timing, Obelisk cash use, extension stall, and late production availability are unrelated policy observations and frozen/out of scope. No valid scratchpad replacement emitted; canonical retained. | Natural enclosure behavior occurred without a scripted wall event: stable 13-cell plan with a three-cell access opening, seven confirmed wall cells, and exact cutoff. The match continued roughly five times beyond cutoff to a real terminal result, proving the policy did not retain reservations or prevent natural completion. | Natural-match acceptance passes. Complete full graphical replay through the recorded literal outcome, report, dependency reinspection, publication, CI, and final Sol-high task-PR review; no further product change is indicated. |

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
