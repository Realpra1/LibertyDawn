# CNC-33 Worker Report

## Result

READY at product commit `215ff53d96a518e0eb1f46594cd62d4fe99652a0`.
The reported split was disabled for Brutalis in practice: although
`EconomyCombatSplit` defaults to `0.5`, only the weighted selector consumes it,
and Brutalis enabled neither that selector nor an economy bucket. The fix adds
only:

- `WeightedUnitSelection: true`
- `EconomyTypes: harv`
- `EconomyCombatSplit: 0.5`

No authored weights, limits, timing, refineries, silos, storage, queue recovery,
VIKI policy, or unrelated balance changed. The intended interpretation is a
50-percent economy route-selection probability when both pools are eligible;
the existing smart-economy income/spend gate may force additional harvester
choices. It is not a 50-percent dollar-spend quota.

## Physical evidence

The inherited modcontent/platform failure remains invalid and uncounted.
Exactly two subsequent ordinary-AI/all-module CNC games qualified:

1. `game1-brutalis-viki-differential`: Brutalis/Nod versus VIKI Control/GDI,
   tick 6000, 34.026 seconds, replay and save, no fatal/desync. The Luna narrator
   confirmed physical smart-economy behavior; the separate Luna policy review
   requested direct routing evidence.
2. `game2-brutalis-viki-nod-instrumented`: both Nod with swapped spawns on the
   distinct Nod-locked variant, tick 8000, 23.024 seconds, replay and save, no
   fatal/desync. A temporary debug-gated trace, removed before final checks,
   recorded 63 decisions (33 economy, 30 combat), every one at `split=0.50`,
   including 30 correctly forced economy routes. The separate Luna narrator
   distinguished decisions from dollar spend; policy accepted the product
   unchanged.

Artifacts are under
`/root/github/.build/coordinated-cnc/20260816-playtest-hotfix/WORKER-2-CNC-33/analysis/`,
including both summaries, `game1-narrator.md`, `game1-policy.md`,
`game2-narrator.md`, `game2-policy.md`, saves, and replays.

## Checks and final gate

- `make check`: passed, 0 warnings/errors.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj -c Release --no-build`: passed
  791/791.
- CNC YAML and both custom-map YAML checks: passed sequentially.
- Harness byte-compilation and `git diff --check`: passed.
- Final native review: READY at `analysis/final-review.md`, no blocker.

The final gate used a fresh Luna-medium turn because native-only constraints and
the fixed completed-thread models made Terra-medium unavailable. No external
Codex process, push, merge, or independent PR was used.
