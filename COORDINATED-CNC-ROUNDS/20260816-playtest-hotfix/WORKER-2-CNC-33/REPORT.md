# CNC-33 Worker Report

## Audit result

The configured 50-percent economy/combat split was disabled for Brutalis in
practice. `EconomyCombatSplit` defaults to `0.5`, but the only consumer is the
weighted selector. Brutalis enabled neither that selector nor the harvester
economy bucket, so every selection used the legacy unweighted path.

The narrow pending change enables `WeightedUnitSelection`, assigns `harv` to
`EconomyTypes`, and explicitly records `EconomyCombatSplit: 0.5`. It changes no
authored unit weights, limits, timing, refineries, silos, storage, queue
recovery, or VIKI policy.

## Evidence and handoff

The inherited control attempt under
`/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-2-CNC-33/analysis/game-1-control/`
is invalid and uncounted: it loaded `modcontent` and failed before world tick 1.
`make check` and `git diff --check` pass. The two required valid custom games,
independent Luna analyses, and Terra final review remain required before this
task can be accepted.
