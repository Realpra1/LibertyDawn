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
