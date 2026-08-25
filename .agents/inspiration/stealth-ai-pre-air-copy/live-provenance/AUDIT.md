# CNC-96A post-instrumentation live-body provenance audit

## Verdict: PASS

Audited the current `integration3` worktree after the debug-only ground
route/HP instrumentation, using the preserved authoritative
`FINAL-MATRIX.json`, the durable prior live map, the archived exact source
spans, and current product/test sources.

| Check | Result |
|---|---|
| Restore-ordered responsibility mappings | PASS — all 98 remain reachable and mapped |
| Live classifications | PASS — 47 `EXACT_BODY`, 5 `RETREAT_FREE_EXTRACTION`, 46 `COMPOSED_INTO_AIR_BODY`; 0 missing/conflicting |
| Matrix authority | PASS — 92 `RESTORE_NECESSARY`, 6 `RESTORE_BETTER`, 51 `KEEP_AIR`, 35 `LEAVE_OUT_RETREAT`; the 98-ID integration order is unchanged |
| Copied-Air authority | PASS — `scripts/check-stealth-ai-air-copy.py` passes identity reversal, and both archived non-owner files exactly match base `0f807a81cf8e9be1b8f6b4c3abd7ad4314223fea` |
| Retreat exclusion | PASS — the five extraction IDs remain retreat-free, the 35 excluded IDs remain outside the integration order, and ground-specialist fences prevent Air flee/retreat entry |
| Ordinary path guard | PASS — both new observations are behind `AirTargetDebugLogging`; its code default is `false` and all 10 CNC AI profile declarations set `false` |
| Focused provenance test | PASS — `dotnet test OpenRA.Test/OpenRA.Test.csproj --no-restore --filter 'FullyQualifiedName~StealthAIFunctionMatrixTest'` (6/6) |

The instrumentation does not alter route generation, danger accumulation,
target scoring, selection, order submission, matrix disposition, or state
transitions. When debugging is disabled, neither ground-log branch evaluates
the added detector-path traversal, HP lookup, or formatting. When enabled,
the branches inspect already-selected route/score facts and write diagnostics
only.

The five checked extraction-sensitive IDs are unchanged:
`CNC96A-FN-039A14EF2BAE`, `CNC96A-FN-44B4F1A3CC3D`,
`CNC96A-FN-E06508589937`, `CNC96A-FN-4099692E50BB`, and
`CNC96A-FN-5F94AED53553`.

## Provenance-map correction

`LIVE-MAP.json` is refreshed beside this audit because the post-log source
line spans moved, not because responsibility mappings changed:

- 35 `StealthAIStateBase.SafeRouteForStealth` mappings now span `676–779`
  (previously `676–748`);
- the `CNC96A-FN-039A14EF2BAE` `StealthAIAttackState.Tick` mapping now spans
  `2954–3283` (previously `2914–3243`).

No product, configuration, or test correction is required.
