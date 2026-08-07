# CNC-41 — Economy Tiberium fields

## Status

Proposed handoff: **First iteration - testing** on
`agent/round-20260807-cnc41-economy-tiberium-fields`. The 20-cycle budget is
exhausted. The safest useful implementation and its passing local gates are
published with a release-blocking route-proof concern and incomplete literal
acceptance explicitly retained. GitHub Windows CI passes; Linux CI is red on one
unused-using style error that cannot be corrected without exceeding the cycle
budget.

## Baseline

- Contract base: `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Initial task head: `ab7997c89b8a2d545b894aef2a08e615e957032e`
- Initial worktree: clean; only the worker-contract commit differs from the base.
- CNC build: passed with 0 warnings and 0 errors.
- CNC YAML validation: passed before product changes.
- Red Dawn map hash: `9773a7c85dbaa2f7ca6471ad9938810982f76d8e`
- Empire Earth map hash: `7e1899fb3dc54edfaee043bcc3a2b89de1c82ecb`
- Archipelago map hash: `5db1ecc4f09e91aad51f4b7adfbe2661d496d437`

## Behavior and design

The opt-in field manager scans configured live blossom-tree identities, owns all
Resonator requests for enabled Economy profiles, maintains deterministic one-to-
one assignments, and serializes one field project behind opening, refinery,
power, repair, and protected-cash recovery. It selects a legal in-range
Resonator footprint, extends build area with configured ordinary Power Plants
only when real engine build-area checks require it, and suppresses generic
Resonator fractions while enabled.

Red projects add a deterministic four-cell perimeter around the tree and planned
Resonator footprint, with an ordered two-cell gate toward a configured stable
building. Field-owned wall production uses exact LineBuild segments and
missing-cell recovery, keeps the gate excluded, blocks Resonator activation until
owned live walls cover every planned cell, and defers no-progress work at the
configured 1500-tick cadence. Completed active identities retain missing-only
maintenance state. Project, queue, retry, extension, perimeter, gate, active
enclosure, and maintenance state is persisted and validated on load, including
exact spatial identity and exact configured perimeter geometry.

The cumulative review found that this is not release-ready: activation
eligibility checks wall coverage and an open gate, but does not first prove a
real ordinary-harvester locomotor round trip through that gate or the reserved
stealth-harvester route. Runtime evidence also never reached an active red
Resonator/maintenance outcome. This is the primary handoff blocker.

## Assumptions and scope

- `split2`, `split3`, `splitblue`, and `splitred` are the configured durable tree
  identities; only `splitred` requires containment.
- Brutalis/WaveMaker's shared Economy BaseBuilder and IronReaper are the intended
  opt-in profiles. Other bots and mods retain the default-disabled generic code.
- Existing stable owned building types can choose a deterministic two-cell gate
  side, but geometry alone is not accepted as traffic proof.
- Existing CNC costs, prerequisites, footprints, power, resource behavior,
  harvest orders, unit reservations, and combat policy are unchanged. The only
  policy literals added are the expressly authorized identity/actor lists,
  four-cell standoff, roughly-six-cell extension target, and 1500-tick cadence.

## Cycle journal

### Cycle 1

Added the first non-red end-to-end slice: opt-in configured-tree scanning,
deterministic one-to-one powered coverage, one serial queue reservation, legal
in-range non-resource placement, core/cash admission, and suppression of the
generic Resonator fraction while enabled. Red trees are detected but deferred to
prevent activation before containment.

Focused checks passed (4/4 new policy tests, zero-warning build, CNC YAML, diff
checks). The first matched Empire Earth pair failed behaviorally: the changed AI
planned `splitblue#503` at `124,22` on tick 1451 but remained blocked by opening
ownership and never reserved or placed a Resonator. The changed run ended naturally
at tick 20000; the exact-base control reached tick 25000; both missed the declared
tick-30000 horizon. The changed run emitted 140 initial discovery lines, which is
bounded but too noisy. No literal outcome passed.

### Cycle 2

Replaced the 140-line scale-map discovery burst with a deterministic initial
type/count summary and added rate-limited admission rejection reasons. A validated
same-build focused pair used ordinary Brutalis and Skynet with normal modules and
a control differing only by the per-bot exclusion toggle. Both runs reached tick
12000. Control passed at 428.048 valid ticks/s; enabled planned `split2#38` at
tick 1 but produced no reservation, placement, or coverage marker. The absence of
an admission-deferred transition proves the Resonator was not in any queue's
buildable set, despite the harness's preplaced upgrade actors. This is a failed
technology/buildability-path test, not field-policy success.

### Cycle 3

Added one bounded diagnostic transition for a Resonator that belongs to the
current queue but is absent from its buildable set; the field manager still does
not bypass prerequisites. The ignored focused maps alone now provide the
`upgrade.economy3` prerequisite. Build, four focused tests, global CNC YAML, and
both map checks passed. In the seed-410341 matched pair, both games reached tick
12000, the control passed, and the enabled run again stopped after scanning 18
`split2` trees and planning tree 38 at tick 1. No reservation, admission,
placement, actor, or coverage followed. This rejects Economy III alone as the
missing production condition and leaves the literal behavior unproven.

### Cycle 4

Preserved legacy refinery counting for non-smart profiles while requiring the
actual unloading-refinery set for smart economy, and tightened field coverage to
normal owner power. The focused pair added two ordinary preplaced `nuk2` actors
to stop low-power recovery from consuming the building queue. Both seed-410441
runs passed at tick 12000. Enabled reserved tree 38 at tick 1, production was
accepted at tick 51, placement was ordered at tick 915, and Resonator 92 at
`82,58` covered and completed the assignment at tick 951. Coverage was released
twice during later low-power intervals and restored after recovery, proving the
new power criterion. This is the first executed non-red project, not red-field or
harvester-route acceptance.

### Cycle 5

Added read-only observation of ordinary harvester cargo increasing near an
assigned live tree and later decreasing at an owned unloading refinery, plus
bounded reasons for a planned project that the queue has not advanced. Enabled
completed tree 38 at tick 951; harvester 122 loaded near that field at tick 2901
and unloaded at refinery 49 by tick 4551, with several other ordinary harvesters
subsequently repeating deliveries. No movement order or reservation was changed.
The next tree-42 plan reported `QueueOrAdmissionPreempted`, and a coverage loss at
tick 6801 named low power before recovery at tick 6901. The enabled run ended
naturally before the declared tick-15000 horizon (launcher maximum 10000; debug
evidence through 11651), while control passed at 15000, so the pair fails its
horizon. Repeated harvest traces are too noisy and must be collapsed.

### Cycle 6

Added deterministic four-cell red perimeter geometry around the selected
`splitred` tree plus the planned two-cell Resonator footprint, with a two-cell
gate on the side nearest a stable configured owned building. Five focused tests
passed after fixing side selection to measure distance to the full side segment.
The red full-engine run identified `splitred` actor 38 at `77,58` and planned 42
wall cells with gate `87,58;87,59`, logging activation blocked. It did not reserve
a Resonator, but it ended naturally before the 12000 horizon. More importantly,
at tick 51 the refresh state machine incorrectly treated `PlanningEnclosure` as
queue-active and retried it into ordinary `Planned`; premature activation became
possible. The geometry slice is proven, but lifecycle safety fails and must be
fixed with explicit wall ownership next cycle.

### Cycle 7

Added field-owned wall reservations, five deterministic LineBuild segments, real
owned-wall completion checks, gate-empty validation, and a hard transition to
Resonator eligibility only after enclosure completion. Both matched games reached
tick 12000. Enabled reserved `sbag`, accepted production, and ordered segment
1's first endpoint at `73,54` on tick 115 with activation still blocked. The
engine consumed that production item after the first endpoint; the manager
incorrectly waited for a second callback from the same item and timed out. This
shows each LineBuild endpoint needs its own serialized wall item. No enclosure or
Resonator occurred, so the safety ordering held while execution failed.

### Cycle 11

When a produced gap wall's reserved exact cell becomes illegal before placement,
the manager now revalidates the current authoritative segment and retargets only
to another legal missing cell on that same boundary. It never moves endpoints,
standoff, or gate, and the existing bounded retry/defer path remains when no
exact-boundary alternative is legal. Focused and full-engine evidence is pending.

Seven focused tests, CNC YAML, and diff checks passed. The exact-seed full-engine
rerun did not exercise retarget because no reserved gap became illegal. Enabled
and control both ended naturally before the tick-12000 target (summary ticks
5000/10000); enabled debug activity reached tick 8251. Resource obstruction
cleared by tick 5251 and no illegal-placement marker recurred, but later queue
preemption left segment 1 incomplete. The recovery helper is retained with only
focused evidence; no behavioral completion claim is made.

### Cycle 12

Completed red projects now retain their exact tree, powered Resonator, wall,
segment, gate, and next-maintenance identities. Every configured 1500 ticks the
manager observes world actors, treats only absent/captured cells as missing,
preserves gate cells, and serializes missing-only replacement through the field
wall owner. A changed/lost tree or powered Resonator releases the active identity
instead of letting another actor inherit unsafe containment. Focused and
full-engine evidence is pending; save/load persistence is the next slice.

Eight focused tests and diff checks passed. The seed-412341 pair with ordinary
Easiest pressure still ended naturally early because the focused opponent lacked
symmetric starting assets (enabled/control summary ticks 10000/5000). Enabled
debug transitions reached tick 10601 but never completed segment 1; one gap became
illegal while no alternative exact boundary cell was legal. No active enclosure
existed, so maintenance remained unexercised and no runtime success is claimed.

### Cycle 13

BaseBuilder save data now includes the exact field project, queue/phase/retry/
defer state, wall segments and gate, active red enclosure identities, next scan,
and maintenance deadlines. Load validates configured/live actors, phase, map
cells, a disjoint gate, and complete segment coverage before restoring. Invalid
state emits an actionable transition and is discarded so live-world scanning can
reconstruct safely. Nine focused tests and focused compilation passed.

A fresh seed-413341 ordinary-AI run saved the exact resource-obstructed
`splitred#38@77,58` project at tick 4000 and reached configured tick 6000. A
separate process loaded that file, emitted `load-restored project=38
active-enclosures=0 next-scan=4001` at tick 4002, resumed exact-boundary gap
orders, and reached configured tick 7000. No second enclosure plan, duplicate
Resonator reservation, invalid-state fallback, desync, or fatal signal occurred.
Both launcher summaries are assertion-failed only because their required save/
exit strings used different wording from the actual automation messages. This
passes mid-project persistence but does not exercise an active enclosure or its
maintenance deadline.

### Cycle 14

Enclosure projects now carry a persisted 1500-tick no-progress deadline and
deferral count. If no configured buildable wall has a legal unresolved cell on
the exact current boundary, the manager releases queue identity, retains the
single project/perimeter/gate and activation block, and re-evaluates only after
the authorized cadence. Critical admission failures retain their own reason and
are not hidden by geometry deferral.

Ten focused tests and focused compilation passed. Fresh seed 414341 passed all
full-engine assertions through configured tick 8000 at 443.872 valid ticks/s.
The first defer occurred at tick 2428 until 3928 with queue ownership released;
the same project then resumed exact segment-1 gap orders at ticks 4451, 4895,
5052, and 5994. A same-segment retarget at tick 4895 retained boundary and gate.
At tick 7919, four unresolved non-resource cells produced a second bounded defer
instead of queue churn. The enclosure still did not complete, so active-state
maintenance remains unproven.

### Cycle 15

Remote physical Resonator sites can now create a serialized
`PlanningExtension` project. The manager selects only configured ordinary Power
Plants on compatible queues, searches a bounded area around the eight nearest
owned buildings, excludes illegal/resource footprints, ranks positive progress
deterministically around the configured six-cell target, and persists the target,
count, and progress. A live plant must expand actual engine build area before the
project advances; later build-radius loss returns retained work to extension.

Eleven focused tests passed. The validated one-tree remote package has OpenRA hash
`d108bac7aea00cf5010bb2e23837110268b84aba`. In both seed-415341 games, the project
placed `nuk2@79,63` and observed it live at tick 351 with four useful cells of
progress toward `resonator@52,54`. Run 1 exposed a wrong-queue deferral from the
Defence queue; this was fixed so only a compatible Building queue may age/defer
Power work. Run 2 emitted no false defer or illegal placement but never received
another compatible Building-queue offer before tick 4000, so the required second
step and final Resonator were absent. Extension is partial, not accepted.

### Cycle 16

Persisted-state validation now permits the normal post-enclosure terminal segment
cursor only for a non-maintenance activation-eligible project whose exact live
wall coverage is complete and whose gate remains open. All incomplete, wrong-
phase, maintenance, negative, and beyond-terminal cursor states remain invalid.
Twelve focused tests pass, including the new persistence boundary matrix; the
CNC-only module build is warning-free, global CNC YAML passes, and the cumulative
diff is whitespace-clean.

Two seed-416341 ordinary-AI games on a validated 41-of-42 prebuilt enclosure map
reached tick 2000 and saved at tick 1000, but neither emitted any field-manager
transition or completed-enclosure marker. The second variant restored the known
test prerequisite provider and used a YAML-valid disabled Resonator prerequisite,
but still did not create an evidenced terminal project. No reload was attempted
because these saves did not prove they contained the state under test. This is
failed harness evidence; the review fix has focused validation only.

### Cycle 17

`PlanningEnclosure` now adopts exact owned live-world segments that are already
complete and advances to the first genuine gap. Stale queue/target state is
cleared when progress is reconstructed; activation eligibility still requires
every planned wall and an open gate. Thirteen focused tests and a zero-warning
module build pass.

The first seed-417341 run revealed that the harness had auto-selected the
repository-root launcher, explaining the absent current-code transitions in
cycle 16 and this run. The corrected explicit-worktree-launcher run scanned 18
configured trees and advanced a normal project, but ranked remote
`split2#39@62,51` ahead of the pre-enclosed `splitred`, so the new recovery branch
remained unexercised. A temporary initialization diagnostic isolated the binary
selection issue and was removed after this run.

### Cycle 18

Candidate commitment now includes only missing paid wall endpoints or retained
gaps observed in the live world. Fully present segments carry zero wall
commitment, and red build-area checks no longer demand construction radius for
walls the player already owns; unresolved walls and the Resonator still require
normal engine legality.

Fourteen focused tests and a clean module build pass. The first seed-418341 run
selected the intended splitred but exposed false extension caused by validating
already-present walls. After the narrow correction, a fresh rerun passed through
tick 1000: it identified `splitred#26@77,58`, reconstructed all five/42 wall
segments at tick 51, preserved gate `87,58;87,59`, became activation-eligible,
and saved the terminal project at tick 500. The disabled Resonator remained
absent, so no premature coverage or completed-project claim is made.

The fresh factual narrative confirms the identical tree and geometry across both
runs and the direct-enclosure correction in run 2. The routine policy reviewer
approved that behavior with high confidence: an already buildable safe enclosure
must not be blocked by unnecessary extension planning. Its residual requirement
for safe live coverage remains unproven and is not inferred from eligibility.

### Cycle 19

Persisted projects and active enclosures now validate spatial identity rather
than actor IDs/types alone: the saved tree cell must match the live tree, the
entire planned Resonator footprint must remain on-map and within effect range,
and an active enclosure's live Resonator must occupy its saved cell. Fifteen
focused tests and a zero-warning module build pass.

A separate process loaded the cycle-18 tick-500 terminal project and passed every
assertion through tick 1500 at 187.312 valid ticks/s. It restored project 26 with
zero active enclosures at tick 501, retained the exact `splitred#26@77,58`
`Planned` wait, and emitted no invalid-load, replan, coverage, completion, desync,
or fatal marker. This is runtime proof for the completed-enclosure terminal
cursor and project continuity, not proof of active-enclosure persistence or live
Resonator safety.

The factual reviewer verified the tick-501 restore, tick-502 scan, tick-752
waiting state, and tick-1500 exit. The policy reviewer returned PASS with medium
confidence: persisted intent plus survival-first deferral is consistent with the
authorized policy. It correctly retains later resume/completion as a functional
evidence gap.

### Cycle 20

Persisted red projects and active enclosures now require the saved wall cells,
ordered two-cell gate, and ordered segments to reproduce the exact configured
four-cell perimeter around the saved tree and Resonator footprint. This prevents
an internally consistent but spatially unrelated ring from becoming accepted
field intent. Sixteen focused tests and a zero-warning module build pass.

A separate current-code reload of the same valid terminal save passed through
tick 1500 at 187.293 valid ticks/s. It restored project 26 with zero active
enclosures at tick 501, retained the `Planned` wait, and emitted no invalid-load,
replan, coverage, completion, desync, or fatal marker. This is compatibility
evidence for legitimate exact geometry; active-enclosure runtime restore remains
unexercised.

## Tests and game evidence

- Pre-change packaged Red Dawn control: seed 410041, Brutalis GDI spawn 1 versus
  Skynet Nod spawn 2, headless MAX, tick 12000, passed with replay and benchmark
  artifacts at `analysis/worker-3-cnc41/baseline-control/run/`.
- Baseline throughput: 315.429 valid ticks/s.
- The resource-modifier cache initialized with zero enabled actors. No configured
  tree assignment, Resonator lifecycle, containment, gate, or maintenance evidence
  appeared, confirming that a clean old-AI run does not provide CNC-41 behavior.
- Cycle-1 matched pair: seed 410141 on Empire Earth, exact-base control at
  `419bee2531d4802bf922c3597b42c6eeb75ab250` versus the changed build, identical
  bots/starts/cash/map. Changed ended at tick 20000 and control at tick 25000;
  neither reached the declared tick-30000 horizon. The field manager planned one
  tree but never admitted construction, so this pair is a failed behavior test.
- Cycle-2 same-build pair: seed 410241, focused enabled hash
  `c323531d3a3d97ee5c44792e71d398af041412ba`, control hash
  `32e939c6f75c889545573395039ad49d6683dc6a`. Both reached tick 12000;
  control passed, enabled failed because no queue/placement/coverage occurred.
- Cycle-3 same-build pair: seed 410341, focused enabled hash
  `3976f339b0b74f6f3a67c4ad74b26d6ae22e1345`, control hash
  `402734a16e32b6dc676d440dafbc576d0c2f6ec3`; both reached tick 12000.
  Economy III was present in both, but enabled again had no queue lifecycle.
  Artifacts: `analysis/worker-3-cnc41/cycle-03-game-03/run/`.
- Cycle-4 same-build pair: seed 410441, focused enabled hash
  `f6d50f2a0a2ea8c2b3b7fb4b0ede4c5c50408490`, control hash
  `04cca3671bf569cf6203474d6af36a28cbd75625`. Both passed through tick
  12000; enabled completed one powered non-red assignment at tick 951 and control
  remained field-policy silent. Artifacts:
  `analysis/worker-3-cnc41/cycle-04-game-04/run/`.
- Cycle-5 same-build pair: seed 410541 on the same focused package hashes.
  Enabled exercised completed coverage, actual cargo gain, owned-refinery unload,
  project-waiting reason, and low-power loss/recovery, but ended before the
  declared 15000-tick horizon; control passed at 15000. Artifacts:
  `analysis/worker-3-cnc41/cycle-05-game-05/run/`.
- Cycle-6 red geometry pair: seed 410641, enabled map hash
  `cc65f4945760ff613c692c69835c839f90fbc9c8`, control hash
  `b2c0b65d18b7fab13b7cfe6c64eb98f2b032a753`. Control passed tick 12000;
  enabled exercised `splitred`, standoff, gate, and activation-blocked markers
  but ended early and exposed a phase-transition defect. Artifacts:
  `analysis/worker-3-cnc41/cycle-06-game-06/run/`.
- Cycle-7 wall-ownership pair: seed 410741, enabled hash
  `79771e625775303e3c5590dc6760a29c5880cfe5`, control hash
  `7030fa363ac81e8ef681557f7155c33e169247b5`. Both reached tick 12000;
  enabled built only the first segment endpoint and failed the lifecycle markers.
  Artifacts: `analysis/worker-3-cnc41/cycle-07-game-07/run/`.
- Cycle-8 endpoint-lifecycle pair: seed 410841 on the same focused hashes. Both
  ended naturally after summary tick 10000 and failed the declared tick-12000
  horizon. Enabled independently reserved/ordered both endpoints but the segment
  did not complete. Map-bin inspection identified resource cells `75,54` through
  `80,54` between the planned top-edge anchors; engine LineBuild stops at the
  first non-buildable cell. Artifacts:
  `analysis/worker-3-cnc41/cycle-08-game-08/run/`.
- Cycle-9 resource-gap pair: seed 410941 on the same focused hashes. Enabled
  reached configured tick 12000; control ended naturally at tick 10000. Enabled
  issued three legal gap orders and observed resource obstruction fall from six
  cells to zero without an illegal-placement retry, but conservative all-actor
  affordability then prevented segment completion. Artifacts:
  `analysis/worker-3-cnc41/cycle-09-game-09/run/`.
- Cycle-10 bounded-budget pair: seed 411041 on the same focused hashes. Enabled
  reached tick 12000 and correctly reported 18 remaining paid orders, but ordinary
  play left only 0-1077 spendable cash against the protected 5000 reserve after
  obstruction cleared; control ended early and is invalid. No safety gate was
  weakened. Artifacts: `analysis/worker-3-cnc41/cycle-10-game-10/run/`.
- Post-cycle-10 provisioned-harness attempt: seed 411142 requested unsupported
  `startingcash 50000`, which fell back to 10,000. Enabled ended naturally with
  summary tick 5000 (debug activity through tick 9001), control at summary tick
  10000, and neither reached the declared 20000 horizon. Enabled retained safe
  activation blocking but never completed segment 1. Both materially judged
  games are invalid harness evidence; they do not start another code cycle.
  Artifacts: `analysis/worker-3-cnc41/cycle-10-game-11-completion/run/`.
- Post-cycle-10 corrected completion pair: seed 411243 used supported 100,000
  starting cash and ordinary VIKI pressure. Enabled reached tick 12000; control
  ended naturally at summary tick 10000. Enabled cleared resource obstruction by
  tick 7601, but planned gap cell `78,54` became illegal at placement three times,
  causing bounded retry/backoff. It was finally ordered at tick 10651, yet segment
  1 did not complete by exit. The result isolates cycle 11 to exact-boundary-cell
  revalidation/retargeting; red activation remained blocked. Artifacts:
  `analysis/worker-3-cnc41/cycle-10-game-12-completion-corrected/run/`.
- Cycle-11 exact-seed retarget pair: enabled/control both ended naturally before
  tick 12000 (summary ticks 5000/10000). The retarget path was unexercised because
  no site became illegal; enabled cleared obstruction and emitted no illegal-site
  retry, but segment 1 remained incomplete before elimination. Artifacts:
  `analysis/worker-3-cnc41/cycle-11-game-13-retarget/run/`.
- Cycle-12 active-maintenance attempt: seed 412341, ordinary Easiest pressure,
  supported 100k cash. Enabled/control ended naturally before the 20000 target
  (summary ticks 10000/5000). The enclosure remained incomplete, so active scan
  and repair were unexercised. Artifacts:
  `analysis/worker-3-cnc41/cycle-12-game-14-active-maintenance/run/`.
- Cycle-13 mid-enclosure save/load: fresh seed 413341 saved at tick 4000 and
  reached tick 6000; the separate reload restored tree 38's project at tick 4002
  and reached tick 7000 with continued exact-boundary work and no duplicate plan
  or invalid load. The summaries' only failures are mismatched required-log
  wording. A preliminary tick-0 load attempt lacking the focused map package is
  excluded from the game count. Artifacts:
  `analysis/worker-3-cnc41/cycle-13-game-15-save-mid-enclosure/`.
- Cycle-14 no-progress recovery: seed 414341, ordinary Brutalis versus Easiest,
  passed configured tick 8000 with a queue-owner-free 1500-tick defer and later
  exact-boundary resume. No duplicate plan or premature Resonator occurred;
  completion remains absent. Artifacts:
  `analysis/worker-3-cnc41/cycle-14-game-16-no-progress/`.
- Cycle-15 remote extension: two fresh seed-415341 games on validated map hash
  `d108bac7aea00cf5010bb2e23837110268b84aba`; both placed one legal configured
  Power Plant for four useful cells by tick 351 and reached tick 4000. The first
  exposed/fixed a Defence-queue ownership bug. The second had no false defer but
  failed the required two-step chain because the compatible Building queue was
  not offered again. Artifacts:
  `analysis/worker-3-cnc41/cycle-15-game-17-extension/`.
- Cycle-16 terminal-save attempt: two seed-416341 games reached tick 2000 and
  saved at tick 1000 on final validated map hash
  `14c231b815840a27fd3ee6e421bfe09cb0f4758a`, but neither produced a field
  project or completed-enclosure marker, so the intended reload was not run.
  Artifacts: `analysis/worker-3-cnc41/cycle-16-game-18-terminal-save/`.
- Cycle-17 existing-enclosure attempt: the first seed-417341 run used the wrong
  repository-root binary; the explicit-worktree-launcher rerun reached tick 1000
  but selected an ordinary remote tree and never exercised the pre-existing red
  perimeter. Artifacts:
  `analysis/worker-3-cnc41/cycle-17-game-19-existing-enclosure/`.
- Cycle-18 red recovery save: run 1 safely exposed false extension; corrected
  run 2 passed the fresh reconstruction/save assertions through tick 1000 on map
  hash `30bfa84d27f7e18a50f81142940536b8eca51935`. Artifacts:
  `analysis/worker-3-cnc41/cycle-18-game-20-red-recovery-save/`.
- Cycle-19 terminal reload: a separate process loaded the cycle-18 tick-500
  save, restored `project=26 active-enclosures=0` at tick 501, retained the
  `Planned` project through tick 1500, and passed at 187.312 valid ticks/s with
  no invalid load, replan, coverage, completion, or desync. Artifacts:
  `analysis/worker-3-cnc41/cycle-19-game-21-terminal-reload/`.
- Cycle-20 exact-perimeter reload: the same legitimate save remained compatible
  after strict geometry validation and passed through tick 1500 at 187.293 valid
  ticks/s with the same safe waiting outcome. Artifacts:
  `analysis/worker-3-cnc41/cycle-20-game-22-exact-perimeter-reload/`.
- Final local gates: strict Debug CNC module build passed with 0 warnings and 0
  errors; the complete OpenRA test assembly passed 471/471; exhaustive
  `./utility.sh cnc --check-yaml` passed all CNC sequences and maps; and
  `git diff --check` passed. GitHub's clean Linux `make check` later found an
  IDE0005 error for unused `using OpenRA.Traits` at manager line 15; Windows CI
  passed. The difference is recorded rather than treating the incremental local
  build as authoritative.

## Control and comparative result

The feature-disabled baseline smoke passed as an engine/control run and retained
the specified old-behavior gap. The strongest successful same-build differential
was cycle 4: enabled completed one non-red powered tree assignment and the
control remained silent at the same tick-12000 horizon. Cycle 5 added one observed
cargo/load-to-refinery unload story but failed its matched horizon. Later red
controls repeatedly ended early or the enabled side never completed containment,
so there is no valid matched red payoff, income, army/survival, winner, or natural
full-match improvement result. No comparative strategic advantage is claimed.

## Performance and determinism

The manager scans configured trees every 50 ticks, maintains one serial project,
sorts all identity/ranking decisions deterministically, bounds extension search
to eight nearest owned buildings and a configured local radius, and rate-limits
unchanged waiting diagnostics. Initial Empire Earth discovery was reduced from
140 per-tree lines to one type/count summary. Focused MAX results include 443.872
valid ticks/s for cycle 14, 166.41 for the fresh cycle-18 reconstruction/save,
187.312 for cycle 19 reload, and 187.293 for cycle 20 reload. These scenarios are
not comparable enough to the 315.429 baseline to establish the required <=5%
sustained regression bound; no performance parity claim is made. No desync was
observed in the recorded current-code runs.

## Reviews

- Baseline factual narrative:
  `analysis/worker-3-cnc41/baseline-commenter/NARRATIVE.md`.
- Baseline routine policy review:
  `analysis/worker-3-cnc41/baseline-policy/POLICY-REVIEW.md`; verdict was no
  policy finding because the control contained no exercised field behavior.
- Cycle-1 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-01-commenter/NARRATIVE.md`; it confirmed early
  planning only and retained the summary/debug tick discrepancy.
- Cycle-1 routine policy review:
  `analysis/worker-3-cnc41/cycle-01-policy/POLICY-REVIEW.md`; it found no
  demonstrated authority violation but judged balance impact inconclusive and
  required direct final construction/access/maintenance evidence.
- Cycle-2 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-02-commenter/NARRATIVE.md`; it confirmed equal
  tick horizons, scan/plan only, and no timing-cost conclusion from available
  batch resolution.
- Cycle-2 routine policy review:
  `analysis/worker-3-cnc41/cycle-02-policy/POLICY-REVIEW.md`; not accepted,
  no unauthorized balance evidence, and actual reservation/placement/powered
  coverage required before policy acceptance.
- Cycle-3 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-03-commenter/NARRATIVE.md`; it confirmed only
  scan/plan at equal tick horizons and no evidenced match outcome, and flagged
  the distinct package identity as limiting strict comparison.
- Cycle-3 routine policy review:
  `analysis/worker-3-cnc41/cycle-03-policy/POLICY-REVIEW.md`; verdict was
  insufficient evidence and failed feature validation. Its full-lifecycle
  evidence recommendation is adopted; suggested safety-threshold tuning is
  deferred because no threshold is evidenced and balance authority is frozen.
- Cycle-4 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-04-commenter/NARRATIVE.md`; it verified actor 92's
  full lifecycle and two recoveries but found no winner, economy, or harvester
  route evidence.
- Cycle-4 routine policy review:
  `analysis/worker-3-cnc41/cycle-04-policy/POLICY-REVIEW.md`; insufficient
  evidence, while recognizing genuine task-directed execution. Its request for
  explicit blocked-project, power-loss, access, and red-field evidence is adopted;
  no strategic improvement is claimed from this smoke.
- Cycle-5 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-05-commenter/NARRATIVE.md`; it reconstructed
  actual harvester 122/268 load-return stories and bounded the enabled terminal
  tick from debug/benchmark evidence while retaining the failed horizon.
- Cycle-5 routine policy review:
  `analysis/worker-3-cnc41/cycle-05-policy/POLICY-REVIEW.md`; mixed. It accepts
  the first field/access behavior but rejects cardinality and strategic claims;
  unrelated SkyNet production remains out of scope.
- Mandatory cycle-5 cumulative review:
  `analysis/worker-3-cnc41/cycle-review-05/CYCLE-REVIEW.md`; its sole advisory
  concern is adopted: configured red trees are permanently deferred, so the next
  implementation slice must be a distinct red enclosure-before-activation path.
- Cycle-6 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-06-commenter/NARRATIVE.md`; it confirms specific
  red-tree selection and geometry, then an unexecuted stall and inconsistent
  terminal evidence.
- Cycle-6 routine policy review:
  `analysis/worker-3-cnc41/cycle-06-policy/POLICY-REVIEW.md`; insufficient
  evidence. It accepts containment-before-activation ordering and requires
  bounded, explicit wall/gate/activation stages; this is adopted.
- Cycle-7 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-07-commenter/NARRATIVE.md`; it confirms the
  matched 12000-tick horizon, the first endpoint order, subsequent retries, and
  absent segment/enclosure/coverage outcomes, with no supported match winner.
- Cycle-7 routine policy review:
  `analysis/worker-3-cnc41/cycle-07-policy/POLICY-REVIEW.md`; unsound/medium for
  retained no-progress intent. Bounded stateful recovery and causal endpoint
  telemetry are adopted. Whole-perimeter replanning is rejected for this failure:
  the first intended LineBuild anchor became occupied and the manager erased its
  progress, so retrying that same anchor—not stale geometry—caused illegality.
- Cycle-8 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-08-commenter/NARRATIVE.md`; it confirms the two
  endpoint productions/orders and absent world completion, while retaining both
  invalid tick-10000 horizons and no supported match result.
- Cycle-8 routine policy review:
  `analysis/worker-3-cnc41/cycle-08-policy/POLICY-REVIEW.md`; mixed/medium. Its
  authoritative world-coverage criterion and bounded missing-only recovery are
  adopted. SkyNet unavailable-production policy is explicitly out of scope.
- Cycle-9 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-09-commenter/NARRATIVE.md`; it confirms four
  accepted wall admissions, resource-obstruction recovery to zero, and the later
  preemption stall, while retaining the invalid/inconsistent control horizon.
- Cycle-9 routine policy review:
  `analysis/worker-3-cnc41/cycle-09-policy/POLICY-REVIEW.md`; mixed/high. Its
  falsifiable liveness rule, bounded next-piece budget, and competing-admission
  telemetry are adopted without overriding survival-critical production.
- Cycle-10 factual paired narrative:
  `analysis/worker-3-cnc41/cycle-10-commenter/NARRATIVE.md`; it confirms the
  enabled tick-12000 red attempt, obstruction clearance, and cash/route deferrals,
  but the control horizon is inconsistent and no outcome comparison is valid.
- Cycle-10 routine policy review:
  `analysis/worker-3-cnc41/cycle-10-policy/POLICY-REVIEW.md`; mixed/medium. Its
  route-invalid replan recommendation is retained for later adversarial evidence.
  Lowering the protected reserve is rejected from this intentionally
  under-provisioned run because the task explicitly protects core recovery.
- Mandatory cycle-10 cumulative review:
  `analysis/worker-3-cnc41/cycle-review-10/CYCLE-REVIEW.md`; its advisory concern
  is adopted. Completed red enclosure identity/gate state, 1500-tick missing-only
  maintenance, and save/load reconstruction are absent and must be added before
  acceptance.
- Post-cycle-10 invalid-harness narrative:
  `analysis/worker-3-cnc41/cycle-10b-commenter/NARRATIVE.md`; it confirms unequal
  early termination and an enabled summary/debug tick conflict, so no strategic
  comparison is valid.
- Post-cycle-10 invalid-harness policy review:
  `analysis/worker-3-cnc41/cycle-10b-policy/POLICY-REVIEW.md`; insufficient
  evidence/high. It supports retaining the 5000 survival reserve and bounded
  long-stall suspension. The manager already releases queue ownership while
  resource cells are blocked; a slower explicit deferred cadence remains adopted
  for cycle 11.
- Corrected completion-run narrative:
  `analysis/worker-3-cnc41/cycle-10c-commenter/NARRATIVE.md`; it verifies the
  enabled tick-12000 sequence and three illegal reservations, while the control
  horizon/result remains invalid.
- Corrected completion-run policy review:
  `analysis/worker-3-cnc41/cycle-10c-policy/POLICY-REVIEW.md`; mixed/high. Its
  recommendation to revalidate and select another legal exact enclosure cell
  after invalid reservation is adopted for cycle 11. Prematurely abandoning the
  live tree or changing balance is rejected.
- Cycle-11 factual narrative:
  `analysis/worker-3-cnc41/cycle-11-commenter/NARRATIVE.md`; it confirms the
  retarget branch was unexercised, the first segment stalled, and terminal tick/
  outcome evidence remained inconsistent.
- Cycle-11 routine policy review:
  `analysis/worker-3-cnc41/cycle-11-policy/POLICY-REVIEW.md`; insufficient
  evidence/high. Bounded recovery plus controlled active-maintenance validation
  is adopted; urgent economy/defense preemption remains, and unrelated VIKI MCV
  churn is rejected as out of scope.
- Cycle-12 factual narrative:
  `analysis/worker-3-cnc41/cycle-12-commenter/NARRATIVE.md`; invalid unequal
  horizons, no active maintenance, and one illegal-site stall with no outcome.
- Cycle-12 routine policy review:
  `analysis/worker-3-cnc41/cycle-12-policy/POLICY-REVIEW.md`; insufficient
  evidence/high. Explicit 1500-tick no-progress defer/reconstruct is adopted once
  save ownership is stable; unconditional field queue priority is rejected.
- Cycle-13 factual narrative:
  `analysis/worker-3-cnc41/cycle-13-commenter/NARRATIVE.md`; it verifies one
  blocked red project resumed after reload, continued exact-boundary gap orders,
  and distinguishes both exact-string launcher assertion failures from engine
  behavior.
- Cycle-13 routine policy review:
  `analysis/worker-3-cnc41/cycle-13-policy/POLICY-REVIEW.md`; policy sound with
  medium-high confidence. Its deterministic persistent-obstruction
  re-evaluation hypothesis is adopted: retain exactly one planned perimeter and
  activation block, defer at the configured 1500-tick cadence, and resume only
  genuinely unresolved cells when they become legal. Weakening containment or
  creating a replacement project is rejected.
- Cycle-14 factual narrative:
  `analysis/worker-3-cnc41/cycle-14-commenter/NARRATIVE.md`; the exact perimeter
  and gate persisted through two bounded deferrals, partial same-boundary work
  resumed, and neither enclosure completion nor Resonator activation occurred.
- Cycle-14 routine policy review:
  `analysis/worker-3-cnc41/cycle-14-policy/POLICY-REVIEW.md`; revise/high. Its
  queue-starvation hypothesis after resource clearance is retained for a stronger
  legal-cell/queue-owner adversarial test, but priority escalation is rejected
  from this trace: tick 7919 explicitly reports `NoLegalEnclosureCell`, and field
  work already precedes discretionary production while correctly remaining below
  power/opening/refinery/repair recovery. No critical owner will be bypassed
  without direct evidence that a legal field item is being starved.
- Cycle-15 factual narrative:
  `analysis/worker-3-cnc41/cycle-15-commenter/NARRATIVE.md`; both runs completed
  one useful configured Power Plant step, and neither showed a second extension
  or final Resonator before tick 4000.
- Cycle-15 routine policy review:
  `analysis/worker-3-cnc41/cycle-15-policy/POLICY-REVIEW.md`; concern/high. Its
  evidence boundary is adopted: the policy has no unconditional second-step or
  tick target. A further plant is appropriate only when actual build-area checks
  show it remains necessary and a legal, survival-safe endpoint is available.
- Mandatory cycle-15 cumulative review:
  `analysis/worker-3-cnc41/cycle-review-15/CYCLE-REVIEW.md`; revise. Its single
  finding is verified and adopted for cycle 16: a completed red enclosure leaves
  the segment cursor at its terminal count, but persisted-state load rejects that
  otherwise valid activation-eligible window and discards the project.
- Cycle-16 factual narrative:
  `analysis/worker-3-cnc41/cycle-16-commenter/NARRATIVE.md`; both runs reached
  tick 2000, requested and produced the tick-1000 save, and exited cleanly, but
  neither evidenced any enclosure or completed-project transition.
- Cycle-16 routine policy review:
  `analysis/worker-3-cnc41/cycle-16-policy/POLICY-REVIEW.md`; inconclusive/high.
  It found no balance-authority breach but requires the next evidence to identify
  a live configured red tree, show containment before activation, and demonstrate
  save/load continuity. This evidence boundary is adopted; no policy success is
  claimed from the clean exits or save files alone.
- Cycle-17 factual narrative:
  `analysis/worker-3-cnc41/cycle-17-commenter/NARRATIVE.md`; only the explicit
  worktree launcher activated current field behavior, which selected and extended
  split2 rather than entering the detected splitred recovery path.
- Cycle-17 routine policy review:
  `analysis/worker-3-cnc41/cycle-17-policy/POLICY-REVIEW.md`; inconclusive/high.
  Its serial-starvation concern is retained as a longer-test hypothesis, but the
  tick-1000 run ended before the configured 1500-tick bounded defer and cannot
  justify overriding critical or existing serial ownership.
- Cycle-18 factual narrative:
  `analysis/worker-3-cnc41/cycle-18-commenter/NARRATIVE.md`; the first run falsely
  extended, while the corrected fresh run reconstructed all five exact segments,
  preserved the gate, and became activation-eligible before saving.
- Cycle-18 routine policy review:
  `analysis/worker-3-cnc41/cycle-18-policy/POLICY-REVIEW.md`; APPROVE/high. Its
  confirmation that already-buildable safe containment must not be blocked by
  extension work is adopted; live coverage remains explicitly unproven.
- Cycle-19 factual narrative:
  `analysis/worker-3-cnc41/cycle-19-commenter/NARRATIVE.md`; exact restore at tick
  501, scan at 502, retained `Planned` wait at 752, and clean tick-1500 exit.
- Cycle-19 routine policy review:
  `analysis/worker-3-cnc41/cycle-19-policy/POLICY-REVIEW.md`; PASS/medium.
  Persisted intent plus survival-first deferral is accepted, while later resume
  and completion remains a functional gap.
- Cycle-20 factual narrative:
  `analysis/worker-3-cnc41/cycle-20-commenter/NARRATIVE.md`; it confirms the exact
  restored waiting transitions and absent forbidden markers. Its apparent source
  path discrepancy is the launcher's normal isolated copy: the manifest names
  the source cycle-18 save and the command loads its copied `input.orasav`.
- Cycle-20 routine policy review:
  `analysis/worker-3-cnc41/cycle-20-policy/POLICY-REVIEW.md`; PASS with limited
  evidence/medium. The waiting state does not prove perimeter traffic, extension
  completion, or missing-only maintenance; this limitation is adopted.
- Mandatory cycle-20 cumulative review:
  `analysis/worker-3-cnc41/cycle-review-20/CYCLE-REVIEW.md`; REQUEST_CHANGES. Its
  sole concern is verified and adopted: wall presence can make a red project
  activation-eligible without a real ordinary-harvester locomotor round trip
  through the planned gate or a reserved stealth-harvester path. Resolving and
  validating this needs cycle 21, so the required disposition is handoff as
  `First iteration - testing`.
- Final Sol-high task-PR review:
  `analysis/worker-3-cnc41/final-pr-review/FINAL-PR-REVIEW.md`; CLEAR for the
  explicit `First iteration - testing` handoff only, not complete or release-
  ready. It agrees that real ordinary and reserved-stealth gate-route proof is
  the highest-impact correctness correction before release.

## Diagnostics

Cycle 1's 140-line initial tree discovery burst was removed in cycle 2. Startup
is now one deterministic type/count summary. Retained bounded transitions name
project identity, admission/queue reason, reservation, placement, coverage,
extension, enclosure, maintenance, defer/retry, and load validity. Unchanged
waiting reasons are rate-limited. A temporary constructor marker used to diagnose
the cycle-16/17 wrong-launcher problem was removed immediately afterward. No cell
dump or per-tick diagnostic remains. CNC enables the bounded field debug channel
for the two opted-in profiles because acceptance and integrated testing still need
these ownership/failure boundaries.

## Deferred work and risks

- Release blocker: require a real ordinary-harvester locomotor round trip from an
  unloading-refinery approach through the exact planned gate to gatherable cells
  and back before red activation eligibility; separately preserve and prove a
  RedTiberiumBomb-reserved stealth-harvester path.
- Complete a red enclosure from ordinary fresh construction, activate exactly one
  powered covering Resonator only after route proof, and exercise breach repair,
  gate-adjacent destruction, extension/anchor loss, and active-enclosure save/load
  across the 1500-tick maintenance boundary.
- Prove the remote extension chain to a final Resonator; current evidence contains
  only one live `nuk2` step with four useful cells and no compatible second queue
  offer before tick 4000.
- Revisit serial starvation after a full 1500-tick defer when an ordinary tree is
  infeasible but a reachable red project exists; do not weaken critical recovery
  priority without direct evidence.
- Run the literal focused three-tree acceptance, three clean post-fix adversarial
  scenarios, connected/island topology, packaged Red Dawn regression, fastest
  natural full match, matched old-control payoff, multi-harvester/gate contention,
  reserved stealth-harvester mission, and <=5% sustained performance comparison.
- The unrelated global queue-manager `failCount += failCount` zero-backoff bug
  remains out of scope; field placement uses its own bounded retry/defer state.

## Final-review response

The coordinator authorized one final-review response limited to the task-scoped
Linux IDE0005 failure. The response removed only the unused
`using OpenRA.Traits` from `BaseBuilderTiberiumFieldManager.cs`; it changes no
behavior, balance, configuration, or route-gating policy. After `make clean`, the
locked repository `make check` passed with 0 warnings and 0 errors, including the
explicit-interface checks. The proportionate filtered
`TiberiumFieldPolicyTest` verification passed 16/16 on
`TargetPlatform=unix-generic`. GitHub Actions run `31170984595` for CI-fix
commit `237da1af47` then passed Linux (.NET 6.0) in 1m57s and Windows (.NET 6.0)
in 3m45s.

## Publication

- Branch: `agent/round-20260807-cnc41-economy-tiberium-fields`
- Intended base: `agent/cnc-20260806-bug-polish-01-release`
- Proposed status: `First iteration - testing`
- Product commit: `aa4e97972d8a0cb7f4780babcdffa4fa363c2299`
- Draft PR: `https://github.com/Realpra1/LibertyDawn/pull/88`
- GitHub checks: final-review response commit `237da1af47`, Actions run
  `31170984595`; Linux (.NET 6.0) passed in 1m57s and Windows (.NET 6.0) passed
  in 3m45s.
- Final Sol-high review: clear for this explicit handoff status only; route proof
  remains required before completion/release.
