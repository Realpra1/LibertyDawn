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
