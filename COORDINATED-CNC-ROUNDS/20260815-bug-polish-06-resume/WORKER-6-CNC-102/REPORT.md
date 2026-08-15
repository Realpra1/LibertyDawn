# CNC-102 cycle 3 report

## Outcome

Proposed status: **Third iteration - testing**.

Cycle 3 established ordinary idle construction queues without changing manager
timings or injecting manager state. The ignored scenarios stage normal cash
during the MCV-only opening tail, then create map-local fixed construction-yard
actors with normal production queues. Both Brutalis and Economy Iron Reaper now
naturally reserve and produce real Resonators before the delayed obstruction.

The new two-bot evidence exposed a second lifecycle failure. Both completed
items start independent 1500-tick ready-only deadlines. Brutalis retains its
item and issues exactly one old simple refinery/resource placement just after
deadline, under friendly SAM coverage. Iron Reaper's completed queue entry
ceases to appear after its first retained-placement poll, so no later queue poll
issues its fallback. Extending project recovery behind the ready deadline stops
the earlier incorrect reset into extension, but selecting persisted ready work
ahead of later queue entries did not recover the missing item.

The distinct legal scenario passed its preload: Brutalis placed at the unchanged
fancy site `43,160`, Iron Reaper started a blocked ready timer, a save was written
at tick 3000, and no simple fallback fired early. The reload leg was not claimed
because the blocked scenario still reproduces the missing completed queue entry.

## Verification

- Affected Debug build with warnings as errors: pass, 0 warnings and 0 errors.
- Focused `TiberiumFieldPolicyTest`: 17/17 pass.
- Global CNC MiniYAML validation: pass.
- Scenario generator Python syntax and `git diff --check`: pass.
- Ordinary two-bot production calibration: pass by tick 2350.
- Legal/save preload: pass to tick 3500 with save artifact and no early fallback.
- Blocked pair: fail at tick 5200; both timers start and Brutalis falls back once,
  but Iron Reaper does not issue a fallback.

Ignored evidence is retained under `.worktrees/cnc102-cycle3/`, notably
`idle-facts-calibration-run/`, `final-fresh-run-fixed/`,
`blocked-fixed-queue-run-2/`, and `blocked-final-pass-run/`.

## Remaining risk and next test

Do not claim acceptance or open a PR. Cycle 4 should instrument the exact queue
actor and all matching `ProductionQueue` entries after Iron Reaper's first ready
poll. Determine whether the completed item migrates to another queue or is
discarded, repair that ownership transition without duplicating production,
then rerun the same blocked pair and saved legal reload.
