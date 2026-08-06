# CNC-39A — Engineer/commando target coordination

## Result

Implemented a per-bot deterministic reservation authority shared by Engineer capture and Commando demolition selection, including compatible exact two-Engineer capture pairs. Autonomous AI C4 now carries a latched safety context from order acceptance through travel, planting, delayed detonation, and bridge/demolishable consumers. Ownership changes to a disallowed relationship cancel or disarm that autonomous action permanently; a later hostile relationship requires a fresh assignment. Explicit manual/forced and authored script demolition remain outside this safety context.

Proposed handoff is **First iteration - testing**. The literal simultaneous-selection and capture-during-travel requirements pass, including a fresh combined regression and both travel/post-plant save-load boundaries. The packet's stronger completion bar also requests a defended natural-production match and an exercised ordinary transport takeover in the final combined scenario; those were not established by the available evidence and remain integration-review risks.

## Design choices

- Reservations are owned by each `CaptureManagerBotModule`; cross-player races are handled by execution-time C4 safety rather than global cross-player state.
- Claim order is stable and purpose-aware: a valid incumbent remains authoritative; a genuinely simultaneous unreserved scan runs capture first; an exact healthy-building Engineer pair is one compatible capture claim; demolition then chooses an unreserved alternate.
- Stalled work is detected from specialist movement or target-health progress. A non-progressing assignment is stopped, its target is deferred for one bounded retry window, and alternate selection can proceed.
- Autonomous C4 is identified by a serialized order marker. One `DemolitionSafety` instance is shared through order resolution, travel, plant, and delayed harmful action. Once invalidated it cannot be reauthorized by a later relationship flip.
- Scripted/manual callers receive no safety object, preserving deliberate friendly demolition.
- No cross-world static state or new per-tick full-world scan was added. Reservation work remains bounded by the module's existing candidates/active specialists; delayed-action safety is O(1).

## Assumptions

- Cross-purpose reservations are intentionally per bot/player. Allied or unrelated bot captures cannot participate in that local reservation authority and are protected by live C4 revalidation instead.
- Save/load replays serialized orders; the autonomous order marker and resulting activity/safety state are reconstructed deterministically by the engine order stream.
- CNC-39 remains the owner of capture thresholds, scoring, husks, healthy-building policy, and reassessment tuning. Its named dependency branch remained at the pinned base SHA through the final pre-publication check, so there was no dependency commit to integrate.

## Cycle count

10 isolated product-code cycles were used. Full-engine game counter: 48, including explicitly labeled invalid harness/setup runs.

## Focused and broad checks

- Focused `CaptureTargetingTest`: 9/9 passed after the final product change.
- Full `OpenRA.Test`: 443/443 passed.
- `make all`: passed, 0 warnings/errors.
- `make check`: passed, including explicit-interface and conditional-trait checks.
- `make check-scripts`: passed.
- `make test`: passed CNC MiniYAML/content validation.
- `git diff --check`: passed.
- No Red Alert, Dune 2000, or Tiberian Sun content was built or tested separately.

Focused additions cover compatible Engineer pair claims, capture/demolition exclusion in both claim orders, and latched relationship/finality safety. Existing target scoring/tiebreak coverage remains green.

## Literal acceptance and old-control comparison

### Simultaneous same-bot selection

Artifact root: `analysis/worker-2-cnc-39a/cycle-3/`
Map SHA-256: `e469891b096c4d9804c2559d3c351a3c8694f8392adde52da7e88a98950d3414`
Seed: 39001.

Changed behavior selected the exact Engineer pair for `weap#157` at tick 1 and the Commando for alternate `nuke#158`. The Engineers captured the shared target by tick 103; the Commando destroyed the alternate by tick 540 and continued to another target. The pinned-base control assigned capture and C4 to the same `weap#157`, demonstrating the original collision.

Narrative/policy: `cycle-3/commenter/NARRATIVE.md`, `cycle-3/policy-review/POLICY-REVIEW.md`.

### Capture during Commando travel

Artifact root: `analysis/worker-2-cnc-39a/cycle-9/`
Map SHA-256: `1ac7fa002ba2e53ae141a304c083cc9470705bba73e0ef2876cfb83332046f7c`
Seed: 39081.

Changed C4 was accepted against enemy `weap#157` at tick 9. An ordinary allied VIKI Engineer captured it at tick 111; the C4 activity explicitly canceled on Ally at tick 112, never planted on that factory, and the factory remained `Multi2` at 53,900 HP through tick 1001. The Commando then accepted a different hostile `fact#159`, proving useful recovery. The changed summary's red status was a target-agnostic plant predicate that saw this later hostile plant; the independent narrative verified the captured target itself had no plant. The pinned control preserved the target in this pre-entry timing because generic hidden-target cancellation also happened to win, so the decisive old-behavior damage comparison is the post-plant race below.

Narrative/policy: `cycle-10/commenter/NARRATIVE.md`, `cycle-10/policy-review/POLICY-REVIEW.md`.

### Post-plant owner change

Artifact roots: `analysis/worker-2-cnc-39a/cycle-5/changed-run-8` and `control-run-8`; decisive earlier differential also at `changed-run-7`/`control-run-7`.
Seed: 39051.

Changed accepted and planted one charge, allied capture completed, and the charge disarmed one tick later; the friendly factory survived at full recorded HP. Pinned control's stale charge destroyed the newly friendly factory. This is the decisive player-visible safety improvement.

Narrative/policy: `analysis/worker-2-cnc-39a/cycle-8b/`.

## Clean adversarial evidence after the final product fix

1. **Repeated ownership ladder and natural conclusion**
   - Artifact: `analysis/worker-2-cnc-39a/cycle-10/ladder-clean-run`
   - Map SHA-256: `12d3b3adb58817c730f2abd58d4dcfabb9822d33aeba7a44e9f60b0784565609`
   - Seed: 39082.
   - C4 canceled on captured `weap#157`, canceled again on captured `fact#159`, then planted/finalized only on continuously hostile `nuke#160`. Both captured targets survived. Natural game over at the hostile target's destruction; final checkpoint tick 2501. Harness passed at about 249.6 world ticks/sec.
   - Reviews: `ladder-clean-commenter/NARRATIVE.md`, `ladder-clean-policy/POLICY-REVIEW.md`.

2. **Deliberate scripted-friendly scope differential**
   - Artifacts: `script-changed-run`, `script-control-run`
   - Map SHA-256: `2b323d7d5ec927742c8b541851d21d43ef7c2f4595644c836c9a6f8c4a0b4d79`
   - Seed: 39083.
   - Both changed and control captured the target, received an explicit authored script demolition order while it was allied, destroyed it, and ended with `target-dead=true`. Changed showed no autonomous plant against that target. This establishes that autonomous provenance did not globally alter scripted demolition.
   - Review: `script-commenter/NARRATIVE.md`, `script-policy/POLICY-REVIEW.md`.

3. **Fresh combined coordination plus ownership race**
   - Artifact: `combined-run-2`
   - Map SHA-256: `52a4a3e7cfd4c9b60455d6d4e4bf9cb114c85734ed4ee92e3062922708d5fd61`
   - Seed: 39084.
   - At tick 1 the exact pair `e6#155+e6#156` captured `weap#158` while `rmbo#157` took alternate `nuke#159`; shared capture completed at 102 and alternate destruction at 539. The Commando then accepted `fact#161`; an allied ordinary Engineer captured it at 792, cancellation followed at 793 before plant, and the target survived. The Commando continued to hostile `nuke#162` and destroyed it. Final checkpoint: both captured targets alive, intended alternate dead. Harness passed at about 239.7 world ticks/sec.
   - The preceding `combined-run` is invalid for the second race because the old map template did not register LuaScript; it still proved the first-half disjoint selection. It is not used as final acceptance.
   - Review: `combined-commenter/NARRATIVE.md`, `combined-policy/POLICY-REVIEW.md`.

## Persistence

### Travel/cancellation boundary

Fresh save artifact: `save-run/ladder-save-tick99`; reload artifact: `load-run-2/ladder-load-tick99`.
Save SHA-256: `2b761f200fa1c230fe7ae7d7b719bd766be1de5321c84a68769c5dd491d59ad7`.

Fresh and reload both preserved the captured first and second targets, canceled their obsolete C4 actions, destroyed only the continuously hostile third target, and ended with the Commando idle at tick 2501. Reload showed outcome-safe timing drift (up to 21 ticks in the third-target completion). The first `load-run` was invalid before world start because the isolated support directory lacked the referenced custom map; `load-run-2` staged the exact map and passed.

Reviews: `persistence-commenter/NARRATIVE.md`, `persistence-policy/POLICY-REVIEW.md`.

### Planted-charge boundary

Fresh save artifact: `postplant-save-run`; reload artifact: `postplant-load-run`.
Save SHA-256: `23ad51e9a33a308fe75f35d7cc9e4d927d90ac946ae800e7e27c604f99b736dc`.

Both fresh and reload accepted at tick 27, planted at 96, captured at 171, disarmed at 172, and observed the friendly target alive at 53,900 HP at tick 751. Neither emitted a final action or destruction for the charge. Both passed to tick 1400.

Reviews: `postplant-persistence-commenter/NARRATIVE.md`, `postplant-persistence-policy/POLICY-REVIEW.md`.

## Contention and recovery

Artifact: `analysis/worker-2-cnc-39a/cycle-7/changed-run-2` versus `control-run-2`.

The changed Commando released blocked `weap#155` as non-progressing at tick 318, selected the reachable alternate at 351, planted at 841, and destroyed it at 887. Control remained committed to the unreachable primary. A post-plant regression rerun after this lifecycle change still disarmed the captured friendly target.

Transport ownership was preserved in code by releasing local assignment/reservation state whenever existing transport reservations claim a specialist; however, the final post-fix evidence did not force an ordinary transport manager to take the exact specialist. This remains a stated integration gap.

## Determinism and performance

- Same-map ladder replicas chose identical specialists, target ActorIDs, purpose order, ownership transitions, hostile plant target, and final outcomes.
- Exact ticks varied between concurrent processes (first capture by 12 ticks; later events by smaller amounts), and travel-save reload completion varied up to 21 ticks. No target/purpose choice changed.
- Clean ladder: ~249.6 world ticks/sec.
- Combined regression: ~239.7 world ticks/sec.
- Script matched pair: changed ~159.625 versus control ~159.629 world ticks/sec, effectively identical.
- Earlier matched travel runs were about 199.7 world ticks/sec for both changed and control.
- No repeatable median regression over 5%, unbounded growth, new GC spike, fatal, or desync was observed. The implementation adds no per-tick world scan.

## Diagnostics

Retained diagnostics are gated by the existing bot debug switch and emit once per meaningful selection/release/C4 transition. They include tick, actor IDs, target owner/relationship, and accept/cancel/plant/disarm/final state. No production-default noisy probe logging or test-only product toggle remains.

## Dependency and publication

Pinned base/intended PR base: `agent/cnc38-early-viki-infantry-rush` at `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`.

The named CNC-39 dependency branch `agent/round-20260806-cnc39-engineer-correction` remained at the same pinned SHA on every check through pre-publication. There were no CNC-39 product commits to inspect or integrate.

Draft PR: `#80` — https://github.com/Realpra1/LibertyDawn/pull/80, targeting
`agent/cnc38-early-viki-infantry-rush`. Local required checks passed; GitHub
checks are awaited after the handoff-metadata push.

## Deferred work

- Force the ordinary VIKI/Iron Reaper transport manager to take an Engineer or Commando during an active claim on both connected and island topology; verify ownership and release in a current integrated build.
- Run a long connected conquest match in which normal production, tech, economy, repair, defenders, squads, and transports naturally produce and exercise both specialists. The natural ladder match used ordinary bots/modules but pre-staged specialists.
- Extend the combined final fixture with real defenders/repair and an observed transport takeover without invalidating its two literal races.
- Add a direct UI force-target/manual C4 black-box input test; authored scripted-friendly scope is already covered.
- Investigate outcome-safe process/reload timing drift if integration determinism checks require exact event ticks rather than stable actor choices and final state.
- CNC-50 owns broader late-game stall recovery beyond this task's bounded reservation release.

## Known failures and risks

- The strongest packet-defined `Complete - testing` bar is not fully met because the final combined run did not force real defenders, repair, or ordinary transport takeover, and the natural match did not rely on production of both specialists.
- A first save reload attempt and several early map fixtures were invalid harness/setup runs; every such run is labeled and excluded from acceptance.
- The changed travel run's broad forbidden predicate flagged a valid later plant on a different hostile target; independent review and the target-specific ladder/combined harnesses corrected the oracle.
- Manual force-target scope is protected structurally by the absence of the autonomous marker but is not directly exercised through UI input.
