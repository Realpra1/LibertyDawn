# Ten-task cumulative playtest integration receipt

## Merge receipt

The integration branch began at
`07784f11899395bec8c909b588898f2048fe1d81` and preserves true merge ancestry for:

- CNC-100 `e1653f9e905742f7b9b659544f3a706e21a2e236` via `56a7f89554`
- CNC-96A `25fb1fb59cdfe018279e4d557f7392a00da23c32` via `4cef124e8f`
- CNC-96B `7cc0d8da915586f399e669825979d33ec67dca2f` via `bdc0e6a3b5`

`git merge-base --is-ancestor` returned success for the base and each exact task
head after conflict resolution.

## Verification receipt

All checks ran from the protected integration worktree and passed:

- `make all`: 0 warnings, 0 errors
- `make check`: analyzer and interface checks passed, 0 warnings, 0 errors
- `make test`: full supported CNC MiniYAML/map suite passed
- `dotnet test OpenRA.Test/OpenRA.Test.csproj -c Release`: 707/707 passed
- `python3 -m unittest tests.test_launch_ai_parallel tests.test_archipelago_readiness_fixtures tests.test_resource_slots tests.test_launch_role`: 39/39 passed
- `git diff --check`: passed

The final staged-file audit excludes raw logs, replays, saves, maps, build output,
the local job envelope, and the ignored `analysis/` evidence tree.

## Full-engine evidence receipt

Exactly two successful games are counted. Earlier launcher/fixture setup attempts
and later exploratory evidence probes are not counted.

### Game 1

- Durable local evidence:
  `analysis/20260813-ten-task-playtest/cycle-1/game1-counted/ten-task-game1-repair/`
- Summary: `status=passed`, exit 0, 2,200 valid ticks, 5.004 seconds
- Runtime map SHA-256:
  `ed173b4642e37d524510a7120762521bbc05594524103ba55a874223b89f2a2e`
- Required evidence matched: compatible facility, queued Repair, health increase,
  full repair, active-squad rejoin, useful post-rejoin action, survivor/replacement,
  two reserved Stealth members, clean final lifecycle state.
- Independent artifacts:
  `analysis/20260813-ten-task-playtest/cycle-1/reviews/game1-commenter/NARRATIVE.md`
  and
  `analysis/20260813-ten-task-playtest/cycle-1/reviews/game1-policy/POLICY-REVIEW.md`.

### Game 2

- Durable local evidence:
  `analysis/20260813-ten-task-playtest/cycle-1/game2-counted-final/ten-task-game2-pressure/`
- Summary: `status=passed`, exit 0, 1,000 valid ticks, 50.093 seconds
- Runtime map SHA-256:
  `2b29080d7cb31bb05d80525835d28afd960558916197a1b51d300f0e3e2fa305`
- Required evidence matched: module shed/reprobe transitions, released/claimed
  registry handoffs, Stealth/Chemical reservation, zero-correction ownership
  audits, Harvester exclusions, bounded telemetry, handoff save, clean teardown.
- Local performance evidence:
  `support/Logs/ten-task-game2-pressure-periodic-stall.tsv` under the game directory.
- Independent artifacts:
  `analysis/20260813-ten-task-playtest/cycle-1/reviews/game2-commenter/NARRATIVE.md`
  and
  `analysis/20260813-ten-task-playtest/cycle-1/reviews/game2-policy/POLICY-REVIEW.md`.

Raw local evidence is intentionally untracked. The concise state and this receipt
are the durable Git evidence for cycle 1.

## Integration cycle 2 receipt

Cycle 2 used exactly two additional distinct successful full-engine games. Invalid
tick-zero content-link setup attempts were stopped and are not counted. The valid
launcher staged installed CNC content from
`/root/github/LibertyDawn/.build/cnc33a/runtime-content`.

### Game A: field commitment and ownership transitions

- Evidence: `analysis/20260813-ten-task-playtest/cycle-2/game-a-counted/cycle2-field-ownership/`
- Seed `962201`; paced rendered; IronReaper Nod versus Brutalis GDI; passed with
  exit 0 and 2,600 valid ticks in 111.217 seconds.
- Runtime map SHA-256:
  `8c609882e7c1eb505ef5dc732c6a546d7b8aeeec5d47c4634a21e46c73482d5f`.
- Both AIs committed economy fields and assigned actual Medium Tank, infantry, and
  Mobile SAM defenders. IronReaper's forced Stealth module load triggered disable,
  released-actor registration, enabled probes, re-shed, and eventual recovery.
- Registry audits after initial reconstruction reported zero corrections through
  the transitions; no Harvester/MCV entered fallback registration or combat orders.
- A 42,591-byte save was created at tick 2,400 with SHA-256
  `f19012e4932b6101e95a53acac3dbfaf1032a6de0a882e2f980e58476568ae49`.
- Periodic stall and bot-module/order telemetry were emitted and the configured
  teardown was clean, with no fatal error, desync, or unhandled exception.

### Game B: post-load continuity

- Evidence: `analysis/20260813-ten-task-playtest/cycle-2/game-b-counted/cycle2-field-post-load/`
- Paced rendered load of Game A's exact save; passed with exit 0 and continuation
  from tick 2,400 through tick 3,200 in 39.058 seconds.
- The staged save SHA-256 matched Game A:
  `f19012e4932b6101e95a53acac3dbfaf1032a6de0a882e2f980e58476568ae49`.
- Registry load evidence reported `overlap=0` for both players. Economy field scan
  phase, assignments, destinations, routes, and order progress restored; field
  compositions and new assignments continued after load.
- Every post-load registry audit reported zero corrections. The disabled Stealth
  module advanced through enabled-probe, probe-pending, and recovered. No Harvester
  entered fallback registration/orders; teardown was clean.
- Performance telemetry captured the load boundary (including a bounded 895.608 ms
  startup/load tick and 253.596 ms world phase) without sustained runaway work.

### Cycle 2 independent analysis

The required counted analyses are fresh `gpt-5.6-luna` medium sessions:

- `analysis/20260813-ten-task-playtest/cycle-2/reviews/game-a-luna-commenter/NARRATIVE.md`
- `analysis/20260813-ten-task-playtest/cycle-2/reviews/game-a-luna-policy/POLICY-REVIEW.md`
- `analysis/20260813-ten-task-playtest/cycle-2/reviews/game-b-luna-commenter/NARRATIVE.md`
- `analysis/20260813-ten-task-playtest/cycle-2/reviews/game-b-luna-policy/POLICY-REVIEW.md`

Earlier Terra analyses are retained locally as supplemental cross-checks only and
are not counted. Luna recommendations for matched strategic controls, temporary
field-protection visibility, and richer board/combat traces are deferred policy-
quality work. They do not contradict the demonstrated ownership continuity or
identify a concrete combined product defect. No cycle-2 product code was changed.

## Final advisory review receipt

A fresh `gpt-5.6-terra` medium integrated-candidate review is stored locally at
`analysis/20260813-ten-task-playtest/release-review/RELEASE-REVIEW.md`. Its sole
advisory identified the 25-to-1,500 field-defense scan interval difference. The
line is owned by included task commit `82bc63f661afa98a55cea9fa3fddf992d93c909e`
and is coupled to that task's low-frequency triggered-control implementation; it
is not a merge resolution or integration-cycle change. The advisory is rejected
as a request to undo reviewed task behavior. The bounded Terra reconsideration at
`analysis/20260813-ten-task-playtest/release-review/RECONSIDERED-REVIEW.md`
returned `clear` with no remaining advisory concern. No product repair or cycle 3
follows.
