# CNC-102 cycle 1 report

## Outcome

Proposed status: **First iteration - testing**.

The scoped port now starts a 1500-game-tick deadline only when the reserved
Resonator production item is complete and being placed. It continues to prefer
the unchanged CNC-41 fancy site. If that site is still illegal at the deadline,
the same ready item is placed through the pre-existing refinery-near-resource
locator. Friendly SAM coverage is a best-effort ordering preference and cannot
withhold an otherwise legal fallback. The deadline is saved and restored with
the field project and is cleared on project retry.

No costs, production durations, actor stats, Tiberium behavior, enclosure
geometry, or construction policy were changed. The only YAML value added is the
task-authorized 1500-tick ready-placement fallback delay for Brutalis and Iron
Reaper.

## Verification

- Affected `OpenRA.Mods.Common` Debug build with warnings-as-errors: pass,
  0 warnings and 0 errors.
- Focused `TiberiumFieldPolicyTest`: 17/17 pass.
- CNC MiniYAML, both generated scenario packages, Python syntax, and
  `git diff --check`: pass.
- Fresh full-engine scenario pair: 2/2 passed to tick 2000, with ordinary
  Brutalis, Economy Iron Reaper, and Skynet, normal modules, headless MAX,
  saves at tick 1500, benchmarks, and replays.
- Blocked scenario reload: passed from the tick-1500 save through tick 4500.
- Fancy scenario reload: passed serially from the tick-1500 save through tick
  4500. One earlier parallel reload is excluded because both engine processes
  contended on the shared `lua/scriptwrapper.lua`; serial retry was clean.

Artifacts are ignored and retained under `.worktrees/cnc102-cycle1/`, notably
`fresh-scenarios-run`, `reload-scenarios-run-2/blocked-ready-reload`,
`fancy-reload-serial-run`, and `discovery-run-9`.

## Remaining failure and risk

The literal runtime acceptance did not pass. Both saved projects restored with
`ready-placement-deadline=0`, which is useful evidence that elapsed time and
save/load do not start the timer before readiness. Neither ordinary target AI
then reserved or produced a Resonator: opening/building queues remained
preemptive, and after opening completion the observed Brutalis admission fell
below protected cash (cash 0 at tick 3274). Consequently the runs did not
exercise a ready obstruction, the 60-second fallback order, legal fancy
placement, duplicate prevention, or post-timeout placement.

The next authorized cycle should retain this implementation and replace only
the scenario setup with a production-faithful route to a naturally ready
Resonator. No PR was opened in this incomplete cycle.
