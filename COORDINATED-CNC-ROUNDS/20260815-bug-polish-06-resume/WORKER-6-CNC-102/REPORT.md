# CNC-102 cycle 2 report

## Outcome

Proposed status: **Second iteration - testing**.

Cycle 2 preserved the ready-only 1500-tick fallback from cycle 1 and made one
additional scoped correction: optional economy-SAM construction is now
considered only after a waiting field project has declined an idle building
queue. This enforces the literal requirement that friendly SAM coverage cannot
delay Resonator work. Opening, refinery recovery, power, air-repair ownership,
field geometry, balance, and Tiberium rules are unchanged.

The ignored scenario generator now supplies a sustained test economy and
bot-specific non-red fields so ordinary Brutalis and Economy Iron Reaper can be
observed independently with all normal modules enabled. No field timing or
manager state is injected.

Runtime acceptance remains incomplete. A calibration run naturally reserved
and produced Brutalis's Resonator: reservation at tick 1649, production
accepted at 1701, and the ready timer started at 2578. A later blocked run
started the timer at tick 3193 with deadline 4693 and issued the old simple
refinery/resource placement at tick 4696, location 37,170, under friendly SAM
coverage. This proves the implementation can retain and release one real ready
item after the full delay. That run exceeded the 120-second harness ceiling by
0.4 seconds and is evidence only, not an accepted scenario.

The two-AI acceptance pair did not pass. Across production-faithful scenario
iterations, dynamic construction-yard enclosure ownership or continuously
occupied ordinary building queues prevented Iron Reaper, and sometimes
Brutalis, from reaching a ready Resonator before the scheduled obstruction.
Save artifacts were created, and pre-readiness restores retained a zero ready
deadline, but the final save/control leg did not contain both required ready
markers. No duplicate or early fallback marker was observed.

## Verification

- Affected Debug build with warnings as errors: pass, 0 warnings and 0 errors.
- Focused `TiberiumFieldPolicyTest`: 17/17 pass.
- Global CNC MiniYAML validation: pass.
- Scenario generator Python syntax and `git diff --check`: pass.
- Two of three parallel no-block same-site seed probes reached tick 3000 with
  ordinary Brutalis, Economy Iron Reaper, and Skynet and retained both projects
  without extension. The third is excluded for concurrent Lua-wrapper
  file-contention failure before game start.

Ignored evidence is retained under `.worktrees/cnc102-cycle2/`, notably
`calibration-run/`, `final-fresh-run-3/`, `final-fresh-run-4/`,
`final-fresh-run-5/`, and `seed-search-run-4/`.

## Remaining risk and next test

Do not claim acceptance or open a PR yet. The next cycle should use the
same-site, bot-specific field layout from the updated generator and establish
an ordinary idle building-queue window for both AIs without changing policy
timings or injecting manager state. Then schedule obstruction only after both
normal productions are accepted, run the blocked pair through both deadlines,
and reload the distinct legal/control scenario through the control deadline.
