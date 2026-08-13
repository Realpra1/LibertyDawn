# Round 04 Safe Release Integration State

## Candidate

- Release branch: `agent/cnc-20260812-bug-polish-04-release`.
- Base used: `origin/bleed` at `06c243f3c329f52ba3216725b0bb21f8fc763030`.
- Candidate head: recorded after the state receipt commit.
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

No integration games have been started; cycle count is **0 / 5**. Each cycle must
use the current candidate head, a fresh Sol-medium focused integration worker,
two distinct full-engine adversarial CNC scenarios, isolated support/content,
fresh per-game commentary and policy review, and combined static checks after any
reviewed task-scoped repair. Do not consume a cycle for a launch that fails before
world tick 1.

| Cycle | Focus | Status |
| --- | --- | --- |
| 1 | CNC-97 transport/capture handoff alongside VIKI and enclosure behavior | pending |
| 2 | CNC-98 construction-state transition and crate/region release under concurrent construction | pending |
| 3 | CNC-107 pending wall route/reload and access preservation alongside ordinary AI pressure | pending |
| 4 | Cross-task resource/order contention, target loss/recovery, and save/reload | pending |
| 5 | Matched full-regression control and final release review | pending |

If any repair is needed, create an owning task-scoped repair branch from the
recorded candidate head, obtain its review, merge it locally with a merge commit,
and update this state with the new candidate, repair head, check results, and
cycle counter. Stop after cycle 5 and retain only the safest proven subset.
