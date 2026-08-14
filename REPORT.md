# CNC-110 task report

## Result

HeavyDrop now removes invalid transport/passenger pairs before each phase and never asks a destroyed transport for `Cargo` or queues a HeavyDrop order to an unusable actor. Discarded pairs release their actor reservations without releasing the valid remainder of the wave. Surviving unboarded passengers immediately release `ReservedCargo` and return to ordinary squad ownership.

Mission thresholds, target selection, routing, timeout values, and balance policy are unchanged.

## Focused verification

- `dotnet build OpenRA.sln --no-restore -v:minimal`: passed, 0 warnings and 0 errors before scenario execution.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --no-restore -v:minimal`: passed, 715/715.
- Regression assertions cover immediate continuation with nine loaded survivors, timeout continuation with eight, mirrored pair release, and preservation of valid pair reservations.

## Full-engine custom scenarios

Generated with `scripts/create-cnc110-scenarios.py`; generated maps, logs, benchmarks, and replays remained outside Git.

- Carrier invalidation: `/tmp/cnc110-batch4/carrier-invalid/summary.json`; passed to tick 2500. Debug evidence records `discarded 1 invalid lifecycle pairs; survivors=1` and `travelling with 9 carriers`.
- Mirrored passenger invalidation: `/tmp/cnc110-batch4/passenger-invalid/summary.json`; passed to tick 1800. Debug evidence records the passenger pair discard, continuation with nine carriers, and later lifecycle pruning during combat.
- Eight-loaded timeout: `/tmp/cnc110-timeout-batch2/batch-summary.json`; passed to tick 1800. Debug evidence records `released 2 unassembled pairs before departure` and `travelling with 8 carriers` after the scenario's 600-tick gather timeout.
- Bounded long match: `/tmp/cnc110-long-batch/batch-summary.json`; shipped `Empire Earth4`, two Brutalis allies versus one VIKI, passed to tick 12000. Required bot/map markers were present; destroyed-trait, exception, fatal Lua, and desync patterns were absent.
