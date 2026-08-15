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

## Independent final-review narratives

Fresh native Terra Commenters read staged regular-file copies of each original artifact set; none received source, task/worker implementation context, this report, or another review.

- Carrier invalidation: `/root/github/LibertyDawn/analysis/20260815-bug-polish-06-resume/worker-8-cnc110/final-evidence/carrier/NARRATIVE.md`; fact-check adopted the run identity, one invalid-pair discard, nine-pair continuation, 2,500-tick clean exit, and absence of the supplied exception. Its inference that loaded `(not in world)` passengers were themselves invalid is rejected: transported cargo is normally out of world, and the nine pairs passed both usable-pair and cargo-membership checks before travelling.
- Mirrored passenger invalidation: `/root/github/LibertyDawn/analysis/20260815-bug-polish-06-resume/worker-8-cnc110/final-evidence/passenger/NARRATIVE.md`; fact-check adopted the immediate invalid-pair discard, nine surviving loaded carriers, threat-aware safe return/replanning, later lifecycle pruning, 1,800-tick clean exit, and absence of the supplied exception. Any inference that the nine loaded passengers were destroyed merely from `(not in world)` is rejected for the same cargo-state reason.
- Eight-loaded timeout: `/root/github/LibertyDawn/analysis/20260815-bug-polish-06-resume/worker-8-cnc110/final-evidence/timeout/NARRATIVE.md`; fact-check adopted release of two unassembled pairs, travel with eight loaded carriers, 1,800-tick clean exit, and absence of the supplied exception. A natural match outcome or completed assault is not required for this deliberately bounded timeout case.
- Bounded long match: `/root/github/LibertyDawn/analysis/20260815-bug-polish-06-resume/worker-8-cnc110/final-evidence/long/NARRATIVE.md`; fact-check adopted the shipped Empire Earth4 setup, two Brutalis allies versus one VIKI, normal multi-AI activity, 12,000-tick clean exit, and absence of destroyed-trait, generic exception, fatal Lua, and desync patterns. No HeavyDrop event occurred, so this remains supplementary broad liveness evidence rather than focused lifecycle proof.

Each narrative received a fresh serialized Terra Policy Review beside it as `POLICY-REVIEW.md`. Their common highest-priority recommendation—phase-aware, once-only removal of actually invalid pairs with exact ownership/reservation release and valid-pair continuation—is adopted and already implemented/tested. Recommendations to reinterpret normally loaded cargo as invalid, alter unload retry/mission policy, require a matched old-behavior control or natural winner, investigate unrelated economy/performance observations, or add further games/instrumentation in this evidence-only response are rejected as category errors, frozen-policy scope expansion, or duplication of the accepted regression and focused-scenario portfolio.

Exact supplied failure anchor preserved: `System.InvalidOperationException: Attempted to get trait from destroyed object (tran 3340 (not in world))`; `HeavyDropTransportManager.IsLoaded` -> `DiscardUnloadedPairs` -> `AdvanceGathering` -> `Tick`.

Fresh independent Terra final review of evidence head `c8e7bef20a`: **ready**, required fix **none**. See `REVIEW.md`.
