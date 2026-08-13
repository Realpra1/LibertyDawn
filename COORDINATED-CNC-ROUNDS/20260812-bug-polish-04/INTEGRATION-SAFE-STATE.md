# Round 04 Safe Release Integration State

## Candidate

- Release branch: `agent/cnc-20260812-bug-polish-04-release`.
- Base used: `origin/bleed` at `06c243f3c329f52ba3216725b0bb21f8fc763030`.
- Candidate head before integration cycle 1: `41bbdb4d6ce4999a5193d86d6d9cc72497722211`.
- Current candidate: this branch's HEAD after the cycle-1 receipt commit; no source
  repair was required.
- Target: `bleed`; this branch must remain a draft cumulative PR and must never
  merge `bleed` directly.
- Publication authority: **not granted**. Do not push this branch or create/update
  a GitHub PR until an authorized publisher receives this receipt.

## Included, verified source heads

| Task | PR | Reviewed source head | Merge commit | CI / merge state |
| --- | --- | --- | --- | --- |
| CNC-97 | #101 | `22144d24e2150cec756e71d858aa39e865e32a8f` | `90940d48c2` | four Linux/Windows checks successful; open draft; CLEAN |
| CNC-98 | #102 | `48a33afb13171e627b6dc17a036df9b268a4c197` | `74ce8a2901` | four Linux/Windows checks successful; open; CLEAN |
| CNC-107 | #103 | `6d2c1faff337fc74fbbb7054975e9afa88976976` | `6801c93e37` | four Linux/Windows checks successful; open draft; CLEAN |

Each source head has common ancestor
`4e12088061ac277c51de2e658dc0209337b80968`. They were merged locally in the
listed order with normal merge commits. The current `bleed` base had advanced by
unsafe CNC-100 commit `06c243f3c3`; all three merges remained clean, including
CNC-107's enclosure tests. No conflict resolution or behavioral reconciliation
was performed.

## Exclusions and risk boundary

- CNC-96 / its PR are excluded: final receipt is unsafe, `First iteration -
  testing`.
- CNC-100 / its PR are excluded as a source merge: final receipt is unsafe,
  `First iteration - testing`. Its commit is already inherited from the current
  `bleed` base, so this candidate cannot represent an absence of that pre-existing
  code. Do not describe CNC-100 as newly integrated or approved by this release.

## Combined checks

- `git diff --check origin/bleed...HEAD`: passed.
- `make check`: passed; Debug build had 0 warnings and 0 errors; interface checks
  passed.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj -c Release --no-restore`: passed.

## Five-cycle integration handoff

Integration cycle count is **1 / 5**. Each cycle must
use the current candidate head, a fresh Sol-medium focused integration worker,
two distinct full-engine adversarial CNC scenarios, isolated support/content,
fresh per-game commentary and policy review, and combined static checks after any
reviewed task-scoped repair. Do not consume a cycle for a launch that fails before
world tick 1.

| Cycle | Focus | Status |
| --- | --- | --- |
| 1 | CNC-97 transport/capture handoff alongside VIKI and enclosure behavior | complete: two valid games; no source repair |
| 2 | CNC-98 construction-state transition and crate/region release under concurrent construction | pending |
| 3 | CNC-107 pending wall route/reload and access preservation alongside ordinary AI pressure | pending |
| 4 | Cross-task resource/order contention, target loss/recovery, and save/reload | pending |
| 5 | Matched full-regression control and final release review | pending |

If any repair is needed, create an owning task-scoped repair branch from the
recorded candidate head, obtain its review, merge it locally with a merge commit,
and update this state with the new candidate, repair head, check results, and
cycle counter. Stop after cycle 5 and retain only the safest proven subset.

## Integration cycle 1 receipt

- Tested exact candidate: `41bbdb4d6ce4999a5193d86d6d9cc72497722211`.
- Runtime setup: canonical `launch-ai-parallel.py`, isolated support directories,
  and installed CNC content staged from
  `/root/github/LibertyDawn/.build/cnc33a/runtime-content`.
- Game 1, `Round04 C1 VIKI Husk and Crate Gate`, seed `40101`: valid VIKI versus
  Brutalis full-engine run, headless MAX, exit 0 at tick 2800 in 7.013 seconds.
  The transport completed the engineer handoff, restored the owned ORCA husk,
  issued a post-restore order, and avoided timeout. With a Fact present, VIKI
  recorded `crates-allowed=False`, `regions-allowed=False`, made no crate or scout
  assignment, and left the visible crate present.
- Game 2, `Round04 C1 Fact Wall and VIKI Gate Pressure`, seed `40102`: valid
  Brutalis/VIKI/Easiest full-engine run, headless MAX, exit 0 at tick 10000 in
  20.032 seconds. Enemy VIKI construction and fixed actors blocked several first-
  Fact ring routes. CNC-107 placed and confirmed legal individual walls,
  preserved access for two-way traffic, logged exact-route deferrals, and issued
  no illegal placements. The intended tick-6000 repair cell had never been built,
  so literal damaged-wall repair was not exercised. The crate remained present at
  the tick-700 result snapshot but was removed at tick 9944 without actor/cause
  attribution; this is insufficient evidence of a CNC-98 gating defect.
- Fresh Luna narration and policy review completed separately for each game.
  Game 1 found the bounded behavior sensible but whole-match impact unmeasured.
  Game 2 judged safe route deferral sensible and treated the repair miss and late
  crate removal as fixture/attribution gaps, not concrete source defects.
- No source repair was made. Balance remained frozen.
- Post-game checks passed: `git diff --check origin/bleed...HEAD`, `make check`
  (Debug, 0 warnings, 0 errors, interface checks), and
  `dotnet test OpenRA.Test/OpenRA.Test.csproj -c Release --no-restore`.
- Conclusion: safe to continue to cycle 2. Cycle 1 is consumed exactly once; do
  not rerun or reinterpret the harness assertion as a third game.
