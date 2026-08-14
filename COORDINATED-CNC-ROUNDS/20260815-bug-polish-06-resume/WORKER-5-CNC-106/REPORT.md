# CNC-106 Worker Report

## Cycle 1 — selective PR117 port

Commit `77d229b12d` ports the queue-stall recovery product and focused policy
checks onto the assigned PR117 base. The recovery manager observes genuine
absence of paid progress under multi-front contention, deterministically retains
the cheapest critical economy front, cancels displaced work through its owning
queue, tracks completion versus invalidation and exit blocking, persists active
state, and releases ordinary work after the selected economy result appears.

The port deliberately excludes old worker state/report history and old commit
`ecbff69355` (`Protect one imminent-threat recovery counter`) because this
assignment excludes a new near-complete threat/combat exception.

### Verification

- `make test`: pass; Release build 0 warnings/errors; CNC MiniYAML pass.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj -c Release --filter FullyQualifiedName~SmartEconomyPolicyTest`:
  45 passed, 0 failed.
- `git diff --check`: pass before commit.

### Full-engine evidence disposition

Two custom CNC games, one with an existing refinery and partial harvester front
and one with no refinery and a partial refinery prerequisite front, both exited
normally at tick 3000. Neither is acceptance evidence. The temporary bases were
underpowered, so the implementation correctly reset stall eligibility to
`low-power`; scheduled cash arrived after that and produced ordinary paid
streaming rather than a genuine eligible stall.

The logs nevertheless confirm healthy non-intervention: the existing-refinery
run changed from eligible to low-power at tick 301 and later streamed multiple
queues; the zero-refinery run likewise entered low-power at tick 301, completed
the refinery through ordinary progress, gained its unloading path, and resumed
parallel construction. No recovery cancellation was claimed.

Raw artifacts remain outside Git at
`/tmp/cnc106-cycle1.S6qnz3/results-xvfb/`. The relevant logs are:

- `existing-refinery-contention/support/Logs/debug.log`
- `zero-refinery-prerequisite/support/Logs/debug.log`
- per-run `summary.json` and batch `batch-summary.json`

Cycle 2 should rerun only corrected, normally powered versions of those two
scenarios and require activation, exact cancellation/refund resolution, selected
completion, and ordinary post-release progress.

## Cycle 2 — powered activation scenarios

Only the temporary full-engine harness changed: each map received an additional
advanced power plant, and cash grants were delayed from 15/35 seconds to 25/45
seconds so the 250-tick no-progress window could mature before funding. No product
or focused-test source changed.

Both authorized ordinary-`brutalis` CNC games exited normally at bounded tick
3000 in about 9 seconds without a fatal Lua error or desync.

The zero-refinery prerequisite case passed all required runtime markers. Recovery
activated exactly once at tick 601 and retained the partial `proc` front. At tick
626 it resolved exactly two displaced cancellations in one event, with
`expected-refund=186` and `earned-delta=186`. The selected refinery visibly
completed and recovery released at tick 1426 with one refinery and its free
harvester. At tick 1476 four ordinary queues logged post-release paid progress;
two ordinary queues then logged completions by tick 1601.

The existing-refinery case remained a non-acceptance scenario, but for a narrower
harness reason rather than power or premature scheduled funding. Its pre-existing
harvester generated intermittent paid progress: stall evidence reached 200 ticks
at tick 551, then fresh income prevented the required 250-tick threshold. Recovery
correctly did not cancel healthy progressing work, and ordinary production later
reached at least five live harvesters. The next harness-only cycle may remove that
initial harvester while retaining the existing refinery, partial queued harvester,
normal power, delayed grants, and viable unloading path.

Raw Cycle 2 artifacts remain outside Git at
`/tmp/cnc106-cycle2.qJgaoP/results/`. The relevant evidence is:

- `zero-refinery-prerequisite/support/Logs/debug.log`
- `existing-refinery-contention/support/Logs/debug.log`
- per-run `summary.json`, plus `batch-summary.json` and `batch-summary.tsv`

## Cycle 3 — existing-refinery acceptance

No product or focused-test source changed. The two temporary scenarios retained
the existing Refinery, usable dock, normal power, partial Harvester queue, and
ordinary Brutalis modules. Their map-local Refinery free-Harvester spawn was
suppressed to isolate income. The first scenario began with no Harvester. The
second began with one explicit Harvester, recorded it at ticks 1 and 51, then
recorded zero live Harvesters at tick 301 after the script destroyed it at the
first second, before any unload.

Both authorized full-engine games passed and exited normally at bounded tick 3000
without a fatal Lua error or desync. `existing-refinery-no-starting-harvester`
finished in 21.054 seconds and `starting-harvester-lost-before-unload` in 18.046
seconds.

Both scenarios produced the same acceptance sequence:

- Exactly 250 ticks of no-paid-progress evidence activated recovery once at tick
  601, selecting the partial `514:Vehicle.GDI:harv` front with 991/1100 remaining.
- Exactly one cancellation-resolution event followed at tick 626. It resolved
  all three displaced entries with `unresolved=0`; `expected-refund=306` exactly
  matched `earned-delta=306`, preserving refund ownership while the selected front
  spent 31 credits.
- The protected Harvester then made continuous paid progress, reached the
  completed/exit wait at tick 1201, and released at tick 1226 with
  `completed=True`, one live Harvester, and one live Refinery/unloading path.
- Ordinary work resumed immediately: infantry paid progress appeared at tick
  1276, then building, defence, vehicle, and the additional building queue all
  logged paid progress by tick 1301. Defence and infantry queues visibly completed
  by tick 1401, satisfying ordinary parallel progress after recovery exit.

Raw Cycle 3 artifacts remain outside Git at
`/tmp/cnc106-cycle3.4tnUP0/results/`. The concise evidence is in
`batch-summary.json` and `batch-summary.tsv`; exact runtime markers are in:

- `existing-refinery-no-starting-harvester/support/Logs/debug.log`
- `starting-harvester-lost-before-unload/support/Logs/debug.log`

Cycle 3 completes the assigned full-engine acceptance action. The implementation
and focused checks remain product/test commit `77d229b12d`; this report/state
update is the durable review handoff.

## Cycle 4 — final-review exit-block repair

The sole final-review finding is resolved. The queue-stall manager now exposes a
single ordinary-production gate that is true while recovery is actively paying
the selected item or while that completed item remains `Done` and exit-blocked.
`BaseBuilderBotModule.QueueStallRecoveryActive` uses that gate, so both adaptive
unit production and empty building queues remain paused until the selected actor
visibly enters the world. Selection, cancellation, refund, balance/config values,
completed queue-item preservation, and the existing save-state representation
are unchanged.

A focused four-state regression covers active recovery, active plus exit wait,
exit wait alone, and fully released state. This specifically proves the recovered
integration boundary: a `Done` selected item still pauses ordinary production,
and the pause ends only when neither recovery state remains.

### Verification

- Focused `SmartEconomyPolicyTest`: 49 passed, 0 failed.
- `make test`: passed; Release build 0 warnings/errors; CNC MiniYAML passed.
- `git diff --check`: passed before handoff.

Exactly two distinct custom full-engine CNC games used fresh seeds and the two
accepted existing-Refinery maps from Cycle 3, each with all normal modules, an
ordinary Brutalis AI, a tick-3000 bound, and a 120-second cap. A preliminary
support-directory bootstrap invocation entered content setup but never started a
game or world and produced zero ticks; it is not a scenario run. After adding the
required runtime-content link, only the following two games ran:

- `exit-block-no-starting-harvester`: no starting income-producing Harvester.
- `exit-block-lost-before-unload`: the starting Harvester transitioned from one
  live actor at ticks 1/51 to zero at tick 301, before its first unload.

Both games accumulated exactly 250 ticks without paid progress and activated
once at tick 601 on the partial Harvester. At tick 626, all three displaced
cancellation groups resolved once with `unresolved=0`; `expected-refund=306`
matched `earned-delta=306`. The protected Harvester made continuous progress,
entered `Done`/exit blocking at tick 1201 while zero Harvesters were live, and
released at tick 1226 only when one Harvester was visibly live with the existing
Refinery/unloading path intact. No ordinary paid progress appeared before release.
Multi-queue infantry, defence, building, vehicle, and refinery progress appeared
from tick 1301 onward; two ordinary queues completed by tick 1351 in the first
game and tick 1376 in the second. Both games reached tick 3000 without fatal Lua
errors or desyncs.

Fresh role-bounded native narration and policy review were performed separately
for each game. The factual narratives confirm the stall, exact refund amount,
exit-block interval, actor-live release, post-release progress, clean tick-3000
exit, and absence of fatal Lua/desync markers. They also preserve two evidence
limits: activation logs four cancellation attempts while the resolution tracks
three entries (all resolved, with the exact 306-credit expected refund), and the
logs show live-Harvester count transitions rather than actor-level unload/route
events.

Both policy reviews found the observed behavior compatible with Liberty Dawn's
economy/survival design and frozen balance. Their highest recommendation is
**advisory**: future diagnostics may label cancellation attempts versus tracked
resolution entries and add actor lifecycle markers when those events are the
fixture subject. Disposition: accept the documentation clarification here, but
make no product, balance, configuration, or additional-game change. The evidence
shows `unresolved=0`, the exact expected refund, release only after one Harvester
is live, and resumed ordinary parallel completions; it establishes no duplicate
refund, premature release, or task-contract violation.

Raw Cycle 4 artifacts remain outside Git at
`/tmp/cnc106-cycle4.GQQzY1/results/`; the exact markers are in each scenario's
`support/Logs/debug.log`, and timing evidence is in the benchmark CSVs.
Fresh analysis artifacts remain outside Git at
`/tmp/cnc106-cycle4.GQQzY1/role-reviews/`: `game-a-commentary.md`,
`game-a-policy.md`, `game-b-commentary.md`, and `game-b-policy.md`.

Fresh Terra final re-review at evidence commit `507c41e38a` returned **READY for
cumulative integration** with required fix `none`. CNC-106 is `Complete -
testing`; no further product or game cycle is indicated.
