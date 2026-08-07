# Worker State: CNC-40

Reread this file after context compaction, before every code-change cycle, after
test results arrive, and before publication. This is the complete assigned work
contract. Do not read the full task sheet, coordinator state, or another worker's
spec. Read applicable `AGENTS.md`. Inspect another worker's named PR commits only
when the dependency section directs it.

## Assignment

- Worker: `WORKER-2-CNC-40`
- Task: `CNC-40 — Adaptive specialists`
- Change category: `AI production evidence and adaptive-production policy`
- Balance authority: `Frozen except for the exact requested completed specialist-outcome evidence and the minimum addition of Engineer (e6) to SkyNet's adaptive eligibility set so that evidence can affect production. Do not change actor costs/stats/prerequisites, authored production weights, confidence, decay, floor, ceiling, intervals, probabilities, or any other numeric policy.`
- Status: `Handoff ready — First iteration - testing`
- Common base branch/SHA: `agent/cnc-20260806-bug-polish-01-release` / `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Task branch: `agent/round-20260807-cnc40-adaptive-specialists`
- Intended PR base: `agent/cnc-20260806-bug-polish-01-release`
- Cycle budget: `20` isolated code-change cycles
- Cycles used: `5`
- Game/build lock directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`
- Game capacity: `2`
- Large-build capacity: `1`
- Task report: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/workers/worker-2-cnc40/COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/WORKER-2-CNC-40/REPORT.md`
- Match-analysis directory: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-2-cnc40`
- Liberty Dawn design reference: `.agents/references/LIBERTY-DAWN-DESIGN.md`
- Full-engine game tests completed: `15` (the prior 12 plus one fresh save run and two reload stages in the final-review response; excluded two tick-0 map-bootstrap failures and one manifest-path parse failure without game evidence)
- Terra cycle code reviews: `cycle 5 complete; its staged-save advisory was adopted and addressed by the final-review test-only response; reachable pre-outcome/post-credit reloads pass, while a captured-but-not-transformed save is impossible because frame-end transformation finishes before the save request boundary; no product change`
- Sol-xhigh policy escalation: `used once after 10 game tests; policy-escalation/POLICY-REVIEW.md recommends First iteration - testing and blocks Complete - testing with medium-high confidence because cycle 4 demonstrates forbidden prolonged Engineer saturation; no further escalation allowed`
- PR: `https://github.com/Realpra1/LibertyDawn/pull/87` (draft; required Linux and Windows checks green on reviewed head `ee5e3aa33b`)

## Integrated repair assignment

- Phase: `integrated testing`
- Current release branch/head: `agent/cnc-20260807-bug-polish-02-release` / `ffb841b48750cc54b1862fb93101d3dce3a87a3f`
- Integration notes: `COORDINATED-CNC-ROUNDS/20260807-bug-polish-02/INTEGRATION.md`
- Repair branch: `agent/round-20260807-cnc40-rc1-repair`
- Repair PR base: `agent/cnc-20260807-bug-polish-02-release`
- Integrated cycles used this RC: `0/3`
- Integrated cycles used total: `0/12`

Before relaunching this worker for combined testing or repair, the integrator must
replace these fields with the exact release head, note path, branch, and counters.
During that phase, the repair branch replaces the original task branch as the
writable branch; the task scope and behavioral contract do not change.

## Why and predicted change

Adaptive production currently learns primarily from ordinary attacker-attributed
kills and losses. Specialist actions create or deny large economic assets through
different completion paths: a Commando plants delayed C4, an Engineer transfers a
building, and an Engineer captures a husk that transforms into a usable vehicle.
Capture and husk restoration do not currently add positive Engineer evidence, and
Engineer is not in SkyNet's adaptive type set. C4 appears to flow through the
generic kill ledger already, but that must be proven because adding a second
completion hook could double-credit it.

After this task, a completed specialist action contributes exactly one positive
sample and the established economic value of its actual final outcome to the
responsible player's specialist type. SkyNet can then retain or increase Engineer
or Commando production after repeated profitable work, while deaths remain normal
negative evidence and unchanged confidence/floor/ceiling policy bounds the
response. From the player's perspective, a building is visibly destroyed or
captured, or a husk visibly becomes a usable owned vehicle; only then may the
specialist's adaptive rating and later selection diverge from the old control.

## Authoritative behavior

Literal task requirement: **Extend adaptive production evidence so Commandos
receive credit for the economic value of buildings detonated and Engineers
receive credit for captured buildings and restored husks.**

- Credit the responsible Commando type/player once when its planted C4 actually
  destroys a building. The total across existing generic kill accounting and any
  new outcome accounting must still be exactly one sample and one building value.
- Credit the completing Engineer type/player once when a building actually
  changes to that Engineer's owner through capture.
- For a captured restorable husk, credit the economic value of the usable actor
  produced by the completed `TransformOnCapture`, not the zero-value shell and not
  shell plus replacement.
- A completed outcome contributes one positive confidence sample regardless of
  value and contributes the non-negative established economic value once.
- Attribute evidence to the specialist actor type (`rmbo` or `e6`) and its player,
  never to the building, husk, replacement vehicle, target owner, assignment owner,
  or another paired/competing specialist.
- Add `e6` to SkyNet's adaptive eligibility set. Leave its authored weight `8`,
  Commando's authored weight `2`, and every adaptive numeric rule unchanged.
- Preserve ordinary specialist loss evidence. Do not manufacture a loss or cost
  subtraction merely because a successful Engineer is consumed by capture, and do
  not suppress a legitimate existing combat death/loss.
- Preserve CNC-39/CNC-39A selection, pair, reassessment, reservation, ownership,
  relationship, transport, and C4-safety behavior exactly.

## Forbidden behavior and failure signals

- No evidence for a request, candidate, reservation, order, movement, arrival,
  planted charge, sabotage-only damage, pending transform, or log line without the
  player-visible completed outcome.
- No credit for canceled/disarmed C4, invalidated relationships/ownership, a
  target destroyed or captured by another actor, failed capture, failed/incomplete
  transform, non-economic target, ordinary Engineer building repair, or ordinary
  Commando gunfire beyond the existing generic combat ledger.
- No duplicate sample/value when C4's `self.Kill(saboteur)` already supplies the
  generic attacker-attributed credit. A delta of two samples or twice the target
  value is a hard failure.
- No full credit to both members of a healthy-building Engineer pair. Only the
  actor whose capture actually completes receives one outcome; total credited
  value remains one building value.
- No wrong-player, wrong-type, stale actor, post-disposal, target-type, or
  replacement-type attribution; no credit after a save/load replay of an already
  completed notification.
- No modification of target scoring, capture health policy, pair policy,
  reservation ownership, retry/reassessment timing, C4 delay/safety, transport
  behavior, specialist stats/cost/prerequisites, authored build weights, adaptive
  confidence/decay/floor/ceiling, or other balance.
- No single lucky high-value outcome causing prolonged specialist saturation that
  crowds out a viable mixed army, anti-armor/anti-air coverage, or economy versus
  matched control. If unchanged adaptive bounds prove insufficient, record a
  policy failure/deferred task; CNC-40 does not authorize tuning them.
- Compilation/rules errors, save incompatibility, desync/nondeterministic
  attribution, per-tick logging, unbounded target/actor scans, allocations in a
  hot loop, or a sustained material MAX-throughput regression are failures.
- Ledger activation logs alone are never acceptance; failure to show the exact
  building destruction/ownership transfer/restored usable actor and subsequent
  bounded production response is a failure.

## Relevant current implementation and control behavior

- `OpenRA.Mods.Common/Traits/Player/PlayerStatistics.cs` owns per-player
  `AdaptiveStats`. `AdaptiveTypeStats` stores built count/value, kills count/value,
  losses count/value, minute kill/loss value, and decayed score. The save format
  serializes those eight integers plus the score. `UpdatesPlayerStatistics.Killed`
  credits the attacker's actor type for a combatant target's `ValuedInfo.Cost`;
  `OnOwnerChanged` only transfers army/assets statistics and creates no adaptive
  success evidence.
- `OpenRA.Mods.Common/Traits/BotModules/UnitBuilderBotModule.cs` rolls minute kill
  and loss values into `DecayedScore`, gates trust by `KillsCount + LossesCount`,
  applies authored weights, and clamps adaptive combat-pool shares. Therefore
  positive value without one positive sample can remain behaviorally inert.
- `mods/cnc/rules/ai.yaml` enables weighted selection only for SkyNet. `rmbo` is
  in `AdaptiveTypes`; `e6` is in `UnitsToBuild` at weight 8 but is absent from
  `AdaptiveTypes`. Commando's weight is 2. No other AI personality uses this
  adaptive type set, so the principal behavioral acceptance bot is SkyNet.
- `OpenRA.Mods.Common/Activities/Demolish.cs` plants delayed actions through
  `Demolishable`; `Demolishable.Tick` ultimately calls `self.Kill(a.Saboteur, ...)`.
  The saboteur can already be disposed/away when the delayed kill occurs. Base-SHA
  control must establish whether this produces exactly one `rmbo` adaptive sample
  and target value before changing the C4 path. Generic kill accounting uses the
  target's `ValuedInfo.Cost`, while specialist targeting uses `GetSellValue`
  (including `CustomSellValue`), so include a custom-value building that can expose
  wrong-value credit even when the sample count looks correct.
- `OpenRA.Mods.Common/Activities/CaptureActor.cs` performs
  `ChangeOwnerSync(self.Owner)` and then sends `INotifyCapture`. Capture does not
  kill the building and currently does not credit the captor's adaptive ledger.
  `TransformOnCapture` responds to that notification by queueing a `Transform` to
  the configured `IntoActor` at forced health.
- CNC husks inherit `Capturable` and define `TransformOnCapture.IntoActor` for
  MCV, harvesters, APC, tanks, artillery, and other vehicles. Their shell lacks
  useful direct value; current `CaptureManagerBotModule.CaptureEconomicValue`
  deliberately uses the greater of target sell value and replacement actor
  custom/value cost. Reuse the owning economic-value policy rather than inventing
  strategic multipliers.
- `CaptureManagerBotModule` from PR #84/CNC-39/CNC-39A assigns `e6` captures and
  `rmbo` demolition, retains healthy-building pairs, reconsiders assignments,
  rejects transport-reserved specialists, persists assignments, and shares
  deterministic purpose reservations. It logs outcome-like assignment retirement
  but is not an authoritative completion boundary and must not become one merely
  because it observes target removal/owner change on a later scan.
- `EngineerRepair`/`RepairBuilding` instantly repairs damaged allied buildings and
  may consume an Engineer. CNC vehicle husk restoration is instead a capture plus
  transform. Allied-building repair is outside this task and must not be credited.
- Relevant history: adaptive kill/loss weighting originated at `3f58b811c0` and
  later SkyNet configuration added Commando adaptive eligibility while leaving
  Engineer non-adaptive. PR #84 head and this base are both
  `419bee2531d4802bf922c3597b42c6eeb75ab250` at specification time.

## Likely wrong approaches and challenges

- Adding unconditional credit in both `Demolishable` and generic kill statistics,
  or assuming the disposed saboteur cannot be attributed without first measuring
  base behavior. Prove the current C4 ledger delta and distinguish `ValuedInfo`
  cost from `CustomSellValue` before selecting a design.
- Crediting in `CaptureManagerBotModule` when an assignment retires as
  `target-removed`/`captured`. That manager polls and restores state, does not own
  all player/manual capture completion, can observe another actor's outcome, and
  can double-credit after load or delayed transform.
- Crediting at `INotifyCapture` without distinguishing a building transfer from a
  restorable husk's eventual successful transform, or trusting a configured
  replacement that never materializes. Evidence requires the final usable actor.
- Using only `KillsValue`/minute value without incrementing exactly one positive
  sample; current confidence uses count. Conversely, treating economic value as
  many samples falsely claims repeated reliability.
- Giving both Engineer pair members full credit, splitting value nondeterministically,
  or selecting a recipient by dictionary iteration rather than actual completion.
- Crediting the post-capture target/replacement's owner/type because the original
  Engineer may be disposed. Capture code still has the completing captor at the
  authoritative boundary; preserve stable type/player identity as needed without
  stale actor references.
- Adding a generic public mutable ledger API with unclear outcome semantics,
  mixing specialist policy into capture/demolition targeting, or expanding the
  already large `CaptureManagerBotModule`. Keep evidence ownership cohesive and
  reusable; keep target policy in its existing module.
- Redefining every capture as a kill, altering observer kills/buildings counters,
  bounty/experience, or loss semantics. Adaptive positive evidence is the only
  requested accounting surface.
- Adding strategic multipliers, remaining-health scaling, denial-plus-acquisition
  double value, caps, normalization, or numeric tuning to make matches look good.
  These exceed frozen balance authority.
- Breaking old saves by changing packed adaptive fields without backward-compatible
  loading. Prefer the existing fields if they express the contract; if state must
  change, explicitly test old-base saves and missing/new fields.
- Relying on unit tests, assignment logs, a passive bot, or a reloaded game as sole
  proof. The observable production consequence needs ordinary real AI in the full
  engine from cycle 1.

## Competing systems and ownership

- `PlayerStatistics` and `UpdatesPlayerStatistics` own adaptive ledger state,
  ordinary built/killed/lost accounting, owner-transfer assets, and persistence.
  New success evidence must compose with, not duplicate, generic kills.
- `CaptureActor`, `Captures`, `CaptureManager`, `INotifyCapture`, and
  `TransformOnCapture` own capture validation, ownership transfer, notification,
  and husk restoration completion. Other capture notifiers may cancel activities,
  give cash/notifications, or transform actors; their order and frame-end timing
  are regression risks.
- `Demolition`, `Demolish`, `Demolishable`, and `DemolitionSafety` own C4 orders,
  planting, delay, relationship revalidation, disarming, and the final kill.
- `CaptureManagerBotModule` owns ordinary-AI target choice, pair decisions,
  retargeting, deferred targets, and shared capture/demolition reservations. It
  must remain a consumer of outcome behavior, not the source of truth for credit.
- `TransportManagerBotModule` and `InfantryAssaultTransportManager` reserve/load
  Engineers and Commandos and can request ground transport production.
  CaptureManager releases its assignments when transport owns a specialist.
  Exercise this contention at least once and prove evidence follows the eventual
  outcome, not reservation owner.
- `SquadManagerBotModule` excludes `e6` and `rmbo` from ordinary combat squads for
  configured AIs, while enemy squads/auto-targeting can intercept them. Preserve
  this ownership boundary.
- `UnitBuilderBotModule` owns weighted/random choice, infantry queues, queued
  counts, cash spend, prerequisites, and adaptive rollovers. `BaseBuilderBotModule`
  can pause production and competes for cash. Harvester, MCV, technology-counter,
  opening-garrison, early-rush, economy-artillery, transport, and supply-aid
  managers issue external requests or consume the same production/cash capacity;
  their ordinary presence is required in integrated games.
- Specialist targets also compete with attack squads, air targeting, support
  powers, sale, repair, capture by another unit/player, owner changes, and target
  destruction. Tests must force at least one race rather than assuming isolation.
- `BuildingRepairBotModule` repairs owned structures; `EngineerRepair` consumes an
  Engineer for allied-building repair. Neither is husk restoration evidence.

## Cross-worker dependencies

- Common base includes CNC-39 and CNC-39A through PR #84 RC4. Inspect their named
  product commits while working and again before publication, without reading
  their worker specs: `53874e4328b8f00ff691d591625d5f548ed1b551`
  (Engineer reassessment), `0e9efa901ae35283d435b217b5498d402b3f9fa9`
  (released surplus Engineer), `0c6accf17aa89be8a6f0a910727a1b289e9b30b0`
  (capture/demolition reservations and C4 safety), and
  `f3fbbb4da48a66739bfc7195a3f3b4f91e5e3d16` (assignment save restoration).
- Material overlap warning: CNC-40 may touch capture/demolition completion or
  `PlayerStatistics`, but it must only consume established CNC-39/39A outcomes.
  Do not edit their target choice, pair/reservation lifecycle, relationship checks,
  or assignment persistence. Before publication, compare PR #84 head with the
  recorded base for changes to `CaptureActor`, `TransformOnCapture`, `Demolish`,
  `Demolishable`, `CaptureManagerBotModule`, `PlayerStatistics`, and
  `mods/cnc/rules/ai.yaml`; rebase/revalidate if those product boundaries moved.
- CNC-90 is a later pending idle-unit/Commando recovery task, not a prerequisite.
  Do not implement recovery here. Record that CNC-90 must preserve exact-once
  credit if it later reissues specialist orders or changes post-mission ownership.
- The claimed CNC-87 orchestration repair has no expected product-code overlap.

If this section names another task PR, inspect that PR's commits while working and
before publication. Do not read its worker spec.

## Spec-time policy consultation

- Proposed-policy narrative: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-2-cnc40/spec-policy/inputs/NARRATIVE.md`
- Sol-high policy review: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-2-cnc40/spec-policy/POLICY-REVIEW.md`
- Verdict and confidence: `sensible; medium confidence because no match evidence exists yet and one high-value outcome may create a sharp response`
- Recommendations adopted as testable hypotheses: `Exact-once responsible-player/type/sample/value attribution is the primary gate; a completed success is one sample plus established value; e6 adaptive eligibility is necessary and in scope; high-value single-success, repeated-success/failure, open-map, defended/interception, cancellation, ownership-race, paired-Engineer, and failed-transform games must test both under-response and mixed-army crowd-out.`
- Recommendations rejected or deferred, with reason: `No material recommendation rejected. Any new caps, normalization, strategic multipliers, or changes to unchanged adaptive numeric policy are deferred because balance is frozen; if current safeguards fail, report the exposed policy problem rather than tune it in CNC-40.`

## Acceptance and tests

### Literal black-box acceptance

In a full-engine CNC match with ordinary SkyNet and all normal modules active, a
player creates three visible economic opportunities: an enemy building reachable
by a SkyNet Commando, a capturable building below the one-Engineer health
threshold, and a restorable vehicle husk. The setup may pre-place the specialists
and nearby targets for the first smoke, but the AI—not a script/passive fixture—
must issue and complete normal C4/capture behavior.

Pass only when the building is visibly destroyed by that Commando's charge, the
second building is visibly owned by SkyNet after that Engineer's capture, and the
husk visibly becomes the configured usable SkyNet-owned vehicle. Evidence must
show exactly one positive sample and the exact established outcome value added to
`rmbo` for the C4 result and to `e6` for each Engineer result, with no target-type
credit or double C4 credit. After a normal rollover and enough ordinary infantry
production choices, changed SkyNet must show a bounded directional increase or
retention of the relevant specialist selection versus the matched old control,
without a mixed-army/economy/survival regression. Requests, reservations, orders,
or ledger logs without all three final observable outcomes do not pass.

### Focused checks and instrumentation

- Before product changes, build a base-SHA probe for all three outcomes. Record
  target `GetSellValue`, any `TransformOnCapture.IntoActor` and its custom/value
  cost, starting `e6`/`rmbo` adaptive fields, generic observer kill deltas, and
  post-outcome fields. This decides whether C4 already passes once and prevents a
  redundant hook.
- Add focused unit/interface tests at the smallest world-independent boundary for:
  one outcome => one positive sample/value; zero/negative value clamping; direct
  building versus transformed actor value; correct player/type attribution;
  duplicate/idempotence behavior if the chosen boundary can be notified twice;
  canceled/failed outcomes; and existing adaptive decay/confidence behavior.
- Add/extend engine-level trait tests where pure tests cannot prove callback order:
  C4 kill after saboteur exit/disposal; capture owner change; `TransformOnCapture`
  completion; two Engineer captors; target stolen/destroyed; and save/load without
  re-credit. Tests supplement games and do not postpone cycle-1 simulation.
- Run the CNC rules/lint gate, targeted NUnit tests including
  `AdaptiveWeightingTest` and `CaptureTargetingTest`, then relevant broad
  `OpenRA.Test`/compile gates. Do not build/test another mod except shared engine
  compilation required by those checks.
- When bounded diagnostics are enabled, one event record must include world tick,
  outcome kind, specialist actor ID/type, specialist player, target actor ID/type
  and pre-outcome owner, direct/replacement value source, credited value/sample,
  ledger before/after, and whether generic accounting supplied the credit. Capture
  and demolition diagnostics must make request, rejection, reservation owner,
  competing consumer, order, plant/enter state, invalidation, transform, and final
  outcome distinguishable through stable actor IDs.
- Emit handled warnings only for genuinely inconsistent states: unknown replacement
  actor, missing required statistics owner, duplicate outcome, wrong relationship,
  or a completed capture whose promised restoration cannot produce a usable actor.
  Do not silently substitute success; zero-value valid outcomes may record zero
  value without spam. Remove noisy temporary probes before publication and leave
  only bounded, configuration-gated diagnostics that are useful in future runs.
- Performance expectation: completed outcomes are rare and should be O(1) ledger
  updates plus bounded trait/value lookup, with no per-tick actor/map scans or
  uncontrolled allocations. Compare matched benchmark CSV/MAX throughput; a
  repeatable >5% valid-world-ticks/second regression or new allocation hotspot
  requires diagnosis, correction, or explicit evidence that noise explains it.

### Ordinary and differential games

All behavioral games use the CNC full engine, ordinary real SkyNet plus an
ordinary active opponent, every relevant normal module, headless MAX, unique
support/log/replay/benchmark paths, and the global game lock. Record commit,
content checksum, focused map checksum/UID, factions, seed, starts, options,
initial actor IDs/types/owners/HP, bot identities, MAX markers, ticks, outcomes,
ledger/weights, production selections/spend, army/economy values, winner, and
simulation cost. A custom map can force opportunities but cannot replace the
normal AI with scripted/passive managers.

1. **Cycle-1 matched smoke/control pair.** Immediately after the first product
   change, run changed branch and base SHA `419bee...` on the same compact open
   map/seed/options. Pre-place one `e6`, one `rmbo`, one below-80%-HP capturable
   building, one destructible enemy building, and one restorable husk in separated
   lanes so normal AI completes all paths quickly. Failure hypothesis: accounting
   fires at intent, misses disposed Commando attribution, credits the shell, or
   duplicates generic C4 credit. Pass evidence is all three final outcomes and
   exact ledger deltas; then continue long enough for rollover and production.
2. **Repeated success with production/cash contention.** Increase distance,
   target count/value diversity, enemy activity, infantry production choices,
   and duration. Do not pre-provide all replacement specialists; allow SkyNet's
   normal queues, tech, BaseBuilder pauses, and external requests to compete.
   Failure hypothesis: evidence exists but never affects selection, or one success
   saturates specialists and harms mixed composition. Pass is repeated profitable
   outcomes, directional but bounded specialist selection versus control, positive
   net specialist economic return, and no material army/economy/survival loss.
3. **Open-map counterplay.** On an open connected map with long approaches, active
   enemy squads can see/intercept specialists. Failure hypothesis: the new policy
   rewards repeated intent despite deaths or keeps producing specialists after
   they cease paying off. Pass is zero success credit for intercepted attempts,
   ordinary losses retained, and response conditional on actual completion.
4. **Defended/interception-heavy counterplay.** Add anti-infantry defense, moving
   threats, and scarce cash/queue capacity. Force TransportManager to reserve or
   load at least one specialist and later release/complete or lose it. Failure
   hypothesis: reservation owner or transport lifecycle gets credit, specialist
   orders conflict, or adaptive production crowds out required counters. Pass is
   outcome-only attribution and a viable mixed army versus control.
5. **Blocked/island topology.** Use Archipelago or a focused island/blocked map
   with one unreachable target and one legitimate transport-delivered specialist
   opportunity. Failure hypothesis: route/transport attempts or stale reservations
   create false evidence, or a delivered completion loses attribution. Pass is zero
   credit for the unreachable attempt and exact outcome credit after a normal
   transport delivery, with no change to transport mission ownership.
6. **Natural-conclusion endurance match.** Run at least one fastest-speed full
   match to natural game over, with long-distance starts and enough tech/time for
   specialist production. Adjust seed/starting opportunities if needed until at
   least one relevant completion occurs; do not pass an unexercised path. Judge
   winner plus specialist ROI, later choices, income/spend, army/assets, useful
   damage, losses, idle queues, and benchmark cost.
7. **Save/load supplement.** Save once after assignment/planting but before outcome
   and once after credit but before/after rollover as useful. Reload on the same
   commit/config, finish the outcome, and verify no lost/double credit and preserved
   adaptive state. Confirm again from a fresh match; no reload is sole acceptance.

Wrap every game command with:

```text
python3 .agents/skills/coordinate-cnc-development/scripts/with_resource_slots.py \
  --lock-dir /root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks \
  --resource game --capacity 2 --slots 1 -- COMMAND...
```

Reserve two slots for a paired `launch-ai-parallel.py` batch. Poll within 60
seconds and normally cap each run at 30 minutes unless a required natural match is
still making useful progress.

### Old-behavior control and required improvement

- Preferred control is a same-build, task-scoped feature-disabled rules override
  if the implementation can expose one without adding shipped policy complexity.
  Otherwise use exact base SHA `419bee2531d4802bf922c3597b42c6eeb75ab250`
  in an isolated read-only/control worktree. The base is authoritative old behavior.
- Keep map bytes/checksum, content, lobby options, factions, bot personalities,
  seed, starts, initial cash/tech/actors/HP/owners, target values, opponents,
  duration/exit condition, and machine concurrency matched. Record unavoidable
  nondeterminism and repeat materially useful pairs rather than comparing different
  AI personalities.
- Primary correctness metrics: outcome count; responsible player/type; exact
  positive sample and value delta per outcome; zero delta per invalidated outcome;
  no duplicate generic/specialist C4 credit; score/confidence/weight after rollover;
  and subsequent `e6`/`rmbo` selection/queue/completion counts.
- Primary policy metrics: specialist economic value acquired/denied minus produced
  and lost specialist value, number/time of subsequent objectives completed,
  mixed-army shares, army/assets value, income/spend and idle queues, survival/win,
  and valid world ticks/second. An isolated action-log delta is not improvement.
- Changed behavior must be identical to control before a completed outcome and on
  all cancellation/failure cases. After repeated valid successes and enough later
  production choices, require a clear directional specialist selection difference
  and at least one additional profitable completed objective or comparably decisive
  net-specialist-value advantage across repeated matched pairs, without materially
  worse economy, army coverage, survival, or throughput.
- Persistent parity after repeated successes, marginal improvement, a loss, or
  one-success specialist saturation is strong evidence of an accounting or policy
  defect. Investigate binding prerequisites, cash/queue contention, confidence,
  floor/ceiling, randomness, and generic C4 credit. Correct in-scope defects or
  provide a concrete task-specific explanation; do not tune frozen numbers.

### Adversarial cases

After normal acceptance first passes and after the latest relevant fix, obtain at
least these three distinct clean full-engine ordinary-AI scenarios. A fix caused
by any failure restarts the affected three-clean-scenario requirement.

1. **Cancellation/ownership/destruction race:** a Commando plants C4, then the
   target becomes allied/neutral, is captured, sold, or destroyed by another
   source before detonation; an Engineer's target likewise changes owner or dies
   before entry. Force both valid and invalid runs. Failure signal is any positive
   specialist delta for an invalidated action or C4 safety/reservations changing.
   Pass is zero invalid credit, exactly one credit for a later genuine outcome,
   and preserved deterministic revalidation/release.
2. **Pair and shared-target contention:** present one >80%-HP building requiring
   two Engineers plus an attractive demolition/capture alternative, while one
   specialist is transport-reserved. Force CaptureManager pair/reassessment and
   capture-versus-demolition reservation logic to act. Failure signal is two full
   Engineer credits, wrong actor/player, reservation-owner credit, deadlock, or
   changed target policy. Pass is one total building value to the actual completing
   Engineer, zero to the partner/Commando, then useful reassignment of survivors.
3. **Husk transformation boundary:** offer direct-value building captures and
   zero-value husks whose replacement actors have low/high/custom values; destroy
   or invalidate one husk during capture and save/load another. Failure signal is
   shell value, shell-plus-replacement, configured-but-unproduced value, duplicate
   post-load credit, or wrong replacement type. Pass is one exact replacement
   value only after a usable owned actor exists, and zero for failed restoration.
4. **High-value response/counterpressure:** one exceptional high-value success,
   then a long defended phase with anti-infantry, anti-armor/anti-air needs, scarce
   cash, and many normal production choices. Failure signal is prolonged specialist
   saturation or weaker economy/army/survival versus control. Pass is a bounded
   temporary response, intact mixed composition, and no unauthorized tuning.
5. **Repeated failure after earlier success:** allow initial profitable outcomes,
   then intercept later specialists. Failure signal is success credit at launch or
   an unresponsive permanent high rating that ignores ordinary losses/decay. Pass
   is exact completed credit, ordinary negative evidence, and conditional later
   production under unchanged policy.

### Final regression

After the latest fix and the clean adversarial set, rerun the literal three-outcome
scenario from a fresh start (not a save) with ordinary SkyNet/opponent and every
normal module. Add the strongest compatible stress: long approach, active enemy
pressure, shared infantry-queue/cash contention, one extra invalidated target, and
continued play through rollover/many later selections. Require exact one-sample/
one-value attribution for C4, captured building, and restored usable vehicle;
zero credit for the invalidated action; unchanged CNC-39/39A reservations and C4
safety; bounded improved specialist production versus the exact matched base
control; viable mixed composition/economy; natural or configured final outcome;
green task/broad checks; and no fatal/desync/performance/diagnostic-cleanup issue.

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
that the harness and simplest behavior work. As soon as it passes, change at least
one meaningful dimension—timing, map geometry, resources, missing/destroyed
assets, unit count, pressure, competing orders, save/load boundary, or match
duration—and make every later test harder or materially different. Never spend
cycles on near-identical happy-path confirmations when a stronger falsification
is possible. These tests replace much human feedback: use surprising results to
challenge the spec's assumptions, inspect the repository/evidence, and choose the
next change without asking the user an implementation question.

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
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/analysis/worker-2-cnc40/cycle-review-05/CYCLE-REVIEW.md`.

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
   Launch a no-history fresh `policy-reviewer` role (Terra 5.6 medium). Questions
   embedded in the narrative are the worker's questions to this playtester; the
   job contains no inline context.
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
seed, map, duration, starting actors/
resources, bots, or focused setup; do not pass an unexercised path. Judge every
unexpected behavior explicitly as acceptable or defective.

Use ordinary full matches for emergent AI behavior. Full-engine real-AI testing
starts in cycle 1 and remains the main feedback loop; increase difficulty as soon
as the first behavior works rather than postponing games until late acceptance.

After normal acceptance first passes, require at least three distinct clean
adversarial scenarios after the latest relevant fix. Every adversarial scenario
must use the full engine, ordinary game AIs, and relevant normal modules. A focused
map may force the edge case, but passive/custom bots or isolated simulations do
not count. Define its expected failure signal, force it to occur, and inspect
current logs/replays; a happy-path rerun is not adversarial evidence.

Include hostile geometry, timing/state transitions, unusual unit counts,
missing/destroyed assets, destruction/capture, save/load where state persists,
and shared resource/order contention as relevant. If a fix follows an adversarial
failure, restart the requirement for three clean adversarial scenarios affected
by that fix, then rerun the original literal acceptance with all normal modules.
Keep that final regression literal, but add the strongest compatible stress
dimension that does not invalidate the acceptance scenario; it must also try to
break the code.

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

Implementation/publication sequence: (1) prove the base C4/capture/husk ledger
behavior; (2) choose the smallest cohesive completion/accounting boundary that
cannot double-credit and add only `e6` adaptive eligibility in owning CNC config;
(3) add focused tests and bounded diagnostic evidence; (4) run the matched cycle-1
full-engine pair and use each harder game/policy review to drive the next cycle;
(5) complete clean adversarial, save/load, natural-match, literal final regression,
performance/determinism, and diagnostic cleanup; (6) write the report, push the
task branch, open one PR to the recorded release branch, wait for checks, and
respond to final review within budget. Forbidden throughout: balance tuning,
target/reservation/recovery changes, another mod, task-sheet/coordinator edits,
direct `bleed` push, PR merge, or acceptance by logs without outcomes.

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
| 1 | Exact completed-outcome ledger boundary for direct capture, replacement completion, and generic C4 value; add `e6` to SkyNet adaptive eligibility. | C4 could double-credit or use the wrong custom value; capture could fire before transfer; husk could credit its shell or configured actor before creation. Perturbation: custom-sell-value refineries, sequential scripted introduction of a second ordinary-AI Engineer/husk, continued play through rollover, exact base control. | Release build 0 warnings/errors; 37 targeted `AdaptiveWeightingTest`/`CaptureTargetingTest` cases pass; CNC YAML, explicit-interface, conditional-interface, and diff checks pass. Base probe: `base-probe/run`. Invalid changed harness outcome run: `cycle-01/changed-run`. Valid matched seed 40601 pair: `cycle-01/paired/{changed,control}`; identical map SHA-256 `48955c11...`, replay seed 40601/map UID `0e22f0...`; throughput 290.994 changed vs 291.058 control ticks/s. | Base factual/policy: `base-probe/commenter/NARRATIVE.md`, `base-probe/policy/POLICY-REVIEW.md`. Pair factual/policy: `cycle-01/commenter/NARRATIVE.md`, `cycle-01/policy/POLICY-REVIEW.md`; policy verdict mixed/medium because exact accounting is sensible but downstream composition is unproven and raw `e6` weight reached 1687.2. No cycle-code review due before cycle 5. | Base C4 produced exactly one sample but used `Valued.Cost` 1500 for a direct-value-400 refinery. Changed valid pair recorded direct capture `e6` 0->1/0->400, restored `htnk` only after creation `e6` 1->2/400->2100, and C4 `rmbo` exactly once via generic accounting 0->1/0->400. Control completed the same visible captures but had no `e6` adaptive entry. Later genuine Commando loss remained. Startup action timing differed because existing `CaptureManagerBotModule` initializes with unseeded `World.LocalRandom`; no desync and replay/map seeds matched. | Keep accounting design. Do not tune frozen numeric policy. Cycle 2 must preserve the ordinary generic-kill path byte-for-behavior where no specialist value applies, handle missing statistics ownership as a bounded warning, then test one-success followed by defended/cash-contended post-rollover production to falsify specialist saturation. |
| 2 | Preserve the original ordinary-kill ledger mutations outside the C4 value override; resolve the direct-capture player at authoritative frame end; skip and warn on the rare missing-statistics inconsistency. | The generic helper could subtly clamp/change ordinary kill accounting; a captor ownership change before frame end could credit the assignment-time player; an absent statistics trait could crash or silently invent credit. Perturbation: one low-value capture only, immediately available infantry factory, normal economy/defense contention, 9000 ticks/two rollovers, exact base control. | Release build 0 warnings/errors; 37 targeted tests pass; CNC YAML passes; diff check passes. Valid matched seed 40602 pair: `cycle-02/paired/{changed,control}`; identical focused-map SHA-256 `064b4d08...`; both tick 9000 with no fatal/desync; throughput 427.863 changed vs 408.453 control ticks/s. | `cycle-02/commenter/NARRATIVE.md`: bounded/non-saturating supported with moderate confidence, but queue loss and combat divergence limit causality. `cycle-02/policy/POLICY-REVIEW.md`: provisional bounded exact-once pass, insufficient behavioral acceptance; no numeric change recommended. No cycle-code review due before cycle 5. | Changed direct capture recorded exactly `e6` 0->1/0->400, rolled once to score 200.50/weight 167.6/confidence 0.10, remained unchanged at the next rollover, and produced no second `e6` credit or warning. Control completed the visible capture without an `e6` ledger entry. Neither run desynced and changed throughput was not regressed. The changed infantry queue was lost before sustained post-rollover choices, while unrelated Rocket Soldier combat diverged sharply, so absence of Engineer saturation is not proof of useful selection response. | Keep the hardening change and frozen policy. Next test must isolate one exceptional-value completion, retain a protected powered infantry producer and ample cash for several rollovers, hold competing infantry choices stable, and remove further valid specialist targets; report under-response/saturation rather than tuning if exposed. |
| 3 | Cache each actor's rare `IAdaptiveKillValue` providers at creation and iterate the fixed array on death, removing the LINQ trait discovery/allocation from every ordinary kill. | The C4 value hook could add a trait/LINQ scan and allocations to the ordinary kill hot path. Perturbation: one 4000-value capture, protected powered infantry production, 12000 ticks, non-capturable base buildings, active combat, exact base control. | Release build 0 warnings/errors; 37 targeted tests and diff check pass. Initial custom-sequence harness failed at tick 0 in both builds and is excluded (`cycle-03/invalid-map-sequences`). Corrected seed 40603 pair: `cycle-03/paired/{changed,control}`, identical map SHA-256 `4e196aa5...`, both tick 12000/no fatal/desync; throughput 499.128 changed vs 544.683 control ticks/s. | `cycle-03/commenter/NARRATIVE.md`: useful but confounded; runtime harvester husks invalidated target-starvation. `cycle-03/policy/POLICY-REVIEW.md`: useful conditional response/provisional pass with medium confidence, material saturation risk still unproven; no current policy failure and no numeric change recommended. No cycle-code review due before cycle 5. | Changed recorded exact direct capture 0->1/0->4000 plus four exact 1100-value usable-harvester restorations, ending 5/8400 with no warning/duplicate. It selected 10 Engineers and 19 other explicitly selected infantry versus control's 3 Engineers/40 others; three changed Engineer attempts were legitimate losses, retry/retarget remained active, and four restorations economically replaced some new harvesters. The response stayed mixed and useful in the target-rich run. Runtime husks defeated the intended no-target premise. Changed throughput was 8.36% lower in this single divergent, higher-actor/log-volume run, unlike the prior matched pairs; treat as non-repeatable pending a stable-workload comparison. | Keep the allocation cleanup and frozen policy. Next probe must remove `Capturable`/`CaptureManager` from runtime husks, create one early high-value completion, preserve a long target-starvation window, then introduce one late valid target. Compare targetless Engineer purchases/crowd-out and retained late responsiveness over repeated seeds; report any persistent failure rather than tune. |
| 4 | Preserve stable Commando planter player/type/ID in the delayed C4 economic-value evidence while leaving generic observer kill ownership and all C4 safety/timing unchanged. | A Commando owner/type change after planting could misattribute delayed adaptive credit to post-mission ownership. Perturbation: remove all ordinary building/tech/husk capture eligibility, protect powered infantry production, one early 4000 outcome, 9051-tick logged target starvation, then one healthy late 400-value building requiring normal pair sabotage/capture. | Release build 0 warnings/errors; 37 targeted tests/diff check pass. Seed 40604 changed/control both reached natural game over before configured tick 15000 and launcher therefore labeled them duration-invalid: `cycle-04/paired/{changed,control}`; identical map SHA-256 `e83c8302...`, no fatal/desync. Changed progressed beyond a tick-14250 spend; control beyond tick 3840; exact final ticks/winner absent. Both are counted as material natural-conclusion tests, not a valid duration-matched isolation. | `cycle-04/commenter/NARRATIVE.md`: material natural evidence, not valid duration-matched isolation. `cycle-04/policy/POLICY-REVIEW.md`: medium-high-confidence Engineer saturation demonstrated, total crowd-out/outcome harm unproven. One allowed Sol-xhigh escalation at `policy-escalation/POLICY-REVIEW.md` blocks `Complete - testing` and recommends `First iteration - testing`; exact accounting remains validated and numeric remedy requires separate authority. No cycle-code review due before cycle 5. | Changed credited the early capture exactly 0->1/0->4000, bought 16 Engineers before late target introduction and 32 overall, reported 25 built/2 lost at tick 12000, and had 13 surplus Engineers rejected around the reserved late pair. The actual pair sabotaged then captured and credited exactly 1->2/4000->4400. Conventional production continued (73 infantry plus economy/defense/tech), so complete crowd-out was not shown; prolonged target-starved specialist saturation was. Control ended before late introduction. | Frozen policy prevents correction. Preserve validated accounting, propose `First iteration - testing`, and defer an explicitly authorized adaptive-production policy investigation. Complete one final hardening/literal regression cycle, cycle-5 review, broad checks, report, PR/checks, and final review without further policy escalation or tuning. |
| 5 | Route C4 through the same handled `TryRecord` boundary as capture/restoration and remove the remaining throwing ledger path. | A missing statistics owner on delayed C4 could crash instead of producing one bounded warning and no false credit. Perturbation: fresh literal three-outcome seed 40601 pair after the final hardening, continuing through a second legitimate C4 and later Commando loss. | Release build 0 warnings/errors; full `OpenRA.Test` 457/457 passes (including 37 focused adaptive/capture tests); CNC YAML and diff checks pass. Final literal pair: `cycle-05/final/{changed,control}`, identical map SHA-256 `48955c11...`, both tick 3500/no warning/fatal/desync; throughput 317.670 changed vs 349.209 control ticks/s. | `cycle-05/commenter/NARRATIVE.md`: PASS focused correctness regression. `cycle-05/policy/POLICY-REVIEW.md`: accounting pass does not alter prior saturation risk; separate authority/evidence required. `cycle-review-05/CYCLE-REVIEW.md`: cumulative design coherent; advisory save/load exact-once coverage concern adopted as a documented handoff risk, with no product change. | Changed direct capture recorded `e6` 0->1/0->400, first C4 recorded `rmbo` exactly once via generic accounting 0->1/0->400, and usable `htnk` restoration recorded `e6` 1->2/400->2100. A later valid C4 credited 4000 once; unplanted removal had no new outcome, and later Commando loss remained. Control completed the same visible paths without Engineer evidence. The short pair's 9.03% throughput gap is not repeatable across the same cycle-1 map (then effectively equal) and coincides with different action ticks/log volume; no sustained regression established. | Scoped accounting is ready for review/publication as `First iteration - testing`. Do not claim the full clean adversarial/final-stress acceptance set: demonstrated saturation blocks completion. Named dependency commits remain ancestors of unchanged base/PR #84 head; publish report and task PR, wait checks, and complete final task-PR review without further policy changes. Save/load was subsequently addressed in the test-only final-review response below. |

Final review response (test-only; no sixth product cycle): Sol-high review
`final-pr-review/PR-REVIEW.md` returned `ready with one fix` for the proposed first
iteration and requested staged save/load exact-once evidence. The response at
`review-save-load` saved a fresh seed-40601 game at tick 600 after direct capture
and C4 but before restoration, reloaded it to complete restoration and a later C4
as exact second samples, saved again at tick 750 after all credits, and reloaded
through the tick-3000 rollover. No specialist outcome re-fired after load tick
751; Engineer remained built 2/killed 2/lost 0 with score 1050.50/weight 1687.2.
The fresh Commenter found no integrity blocker; routine Policy Review was mostly
sensible/medium. The captured-but-not-transformed subcase is rejected with engine
ordering evidence: same-tick frame-end capture/`AfterTransform` completes before
`TryAutomatedSave`, so no valid save can contain that transient interval. No code,
config, or balance change was made; cycles used remains 5.

## Handoff receipt

- Proposed status: `First iteration - testing`
- Final branch/head: `agent/round-20260807-cnc40-adaptive-specialists`; reviewed product/report head `ee5e3aa33bf58b902af5a0803beddbdd9bb80a3b` plus a receipt-only handoff update
- PR and checks: `draft PR #87 https://github.com/Realpra1/LibertyDawn/pull/87; Linux passed 2m10s and Windows passed 3m16s on ee5e3aa33b; receipt-only update requires final rerun`
- Cycles used: `5/20`
- Acceptance evidence: `exact one-sample/value direct capture, usable replacement restoration, and delayed C4 attribution passed in fresh full-engine ordinary-AI games; e6 eligibility affects later selection; broad tests and CNC rules pass`
- Adversarial evidence: `custom sell value, delayed/disposed planter, actual frame-end captor, target-rich repeated restoration/loss, target-starvation and late healthy pair, unplanted target removal, active economy/production contention, and reachable pre-outcome/post-credit save/load boundaries exercised; full clean cancellation/transport/failed-transform set not complete`
- Old-behavior control and comparative result: `exact base 419bee2531 with matched maps/seeds/options; control completes visible outcomes but never records Engineer evidence, while changed records exact outcomes; changed production responds, but a 4000-value success caused prolonged target-starved Engineer saturation`
- Match narratives and routine policy-review conclusions: `base-probe and cycle-01 through cycle-05 under analysis/worker-2-cnc40; accounting passes, downstream policy remains conditional, cycle 4 demonstrates saturation, cycle 5 is a focused correctness pass only`
- Terra cycle code reviews and dispositions: `cycle-review-05/CYCLE-REVIEW.md; advisory save/load concern adopted; reachable boundaries pass in review-save-load, captured-before-transform subcase rejected as impossible at the after-World.Tick save boundary; no product change`
- Sol-xhigh policy escalation (unused, or test count/path/conclusion): `used once after 10 counted games at policy-escalation/POLICY-REVIEW.md; medium-high confidence; recommends First iteration and blocks Complete because numeric policy is frozen and prolonged saturation is demonstrated`
- Final Sol-high task-PR review and disposition: `final-pr-review/PR-REVIEW.md returned ready with one fix; the save/load finding was adopted through the one permitted test-only response, reachable pre-outcome/post-credit boundaries pass, and the requested captured-before-transform save point is rejected because no valid save boundary exists before same-tick AfterTransform`
- Final regression: `cycle-05/final changed/control seed 40601; direct capture 400, restoration 1700, C4 400 then later C4 4000 all exactly once; invalid removal no credit; ordinary Commando loss preserved; no warning/fatal/desync. Review response then proved reachable save/load continuity and no post-credit re-fire through rollover.`
- Error/warning and diagnostic-cleanup result: `release build 0 warnings/errors; OpenRA.Test 457/457; CNC YAML passes; bounded outcome/inconsistency diagnostics retained, temporary probes/raw outputs excluded`
- Performance/determinism result: `no desync; identical cycle-1 workload essentially equal throughput; slower cycle-3/cycle-5 pairs were not repeatable and had divergent actions/actor/log volume, so no sustained material regression established`
- Deferred work: `authorized adaptive-policy investigation for Engineer saturation; direct serialized-ledger save/load assertion if higher confidence is required; persist pending identity if transformation is ever deferred across ticks; remaining clean adversarial/transport/final-stress set; stable-workload performance repeat; CNC-90 exact-once preservation`
- Known failures/risks: `prolonged Engineer saturation after one exceptional success; direct serialized save-state dump absent despite passing log/rollover continuity; in-game post-plant Commando owner-change not forced; full acceptance set incomplete`
- Relevant artifact paths: `analysis/worker-2-cnc40/{base-probe,cycle-01,cycle-02,cycle-03,cycle-04,cycle-05,cycle-review-05,policy-escalation,final-pr-review,review-save-load}; task REPORT.md`
