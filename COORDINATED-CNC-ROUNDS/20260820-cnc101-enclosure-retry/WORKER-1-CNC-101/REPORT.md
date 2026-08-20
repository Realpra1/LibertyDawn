# CNC-101 Cycle-1 Report

## Proposal

`Complete - testing`

The protected secondary construction queue now waits for the bound initial Construction Yard enclosure to be physically complete, or for a bounded eight-retry escape, instead of releasing after four unrelated walls. The change is confined to the CNC-101 enclosure/opening policy and its focused tests; balance values and unrelated construction policies are unchanged.

## Root cause and correction

The previous release condition used `CompletedWallCount >= 4`. That count was global and did not establish that the first Construction Yard's planned 16-cell ring was closed. Missing or temporarily illegal placements had no bounded initial retry state, so normal secondary construction could either release early after four walls or wait indefinitely.

The correction introduces a persisted initial-enclosure phase and retry count:

- a planned cell is satisfied by an owned wall or terrain that the wheeled locomotor cannot traverse;
- missing legal cells continue through the existing exact-cell placement path;
- a placed-but-unconfirmed endpoint is retried at most once per maintenance interval, so competing queue polls do not consume retries or create duplicate orders;
- no-legal-cell observations and due issued-cell retries saturate at eight;
- the initial phase releases only on physical completion or the explicit retry limit;
- the secondary queue then retains its existing Silo, configured first defense, normal construction policy;
- save data advances to version 4 while accepting versions 2 and 3 and validating the restored retry bound.

An initial diagnostic terrain run exposed that an earlier draft counted every competing poll of an in-flight endpoint as a retry. That run is excluded from acceptance, the accounting was corrected, and the 249/250-tick maintenance boundary is covered by regression. A later control-scenario calibration reached tick 9000 and proved enclosure completion but VIKI did not exercise the required later construction markers; it too is excluded. It prompted only an ordinary-AI selection correction, not a product change. Raw diagnostics remain ignored with the other game artifacts.

## Checks

- Focused: `dotnet test OpenRA.Test/OpenRA.Test.csproj --filter 'FullyQualifiedName~ConstructionYardEnclosurePolicyTest|FullyQualifiedName~OpeningPolicyLogicTest' --no-restore --verbosity minimal` — **45 passed, 0 failed**. Four unrelated pre-existing analyzer warnings appeared in other test files.
- Protected: canonical large-build resource wrapper plus `make check` — **passed**, build **0 warnings, 0 errors**, explicit interface checks passed.
- `./utility.sh cnc --check-yaml` — **passed** for the CNC mod and maps.
- `./utility.sh cnc --check-yaml .build/20260820-cnc101-enclosure-retry/games/cnc101-terrain.oramap` — **passed**.
- `./utility.sh cnc --check-yaml .build/20260820-cnc101-enclosure-retry/games/cnc101-control.oramap` — **passed**.
- Embedded scenario Lua preflight — **passed** for both maps.
- `git diff --check` — **passed**.

## Qualifying game evidence

### Game 1: terrain-imposed enclosure hole

- Artifact root: `.build/20260820-cnc101-enclosure-retry/game1-qualified/cnc101-game1-terrain`
- Ordinary all-module AIs: target Brutalis/GDI versus VIKI/Nod; headless MAX; seed 1018201.
- Exit 0 at tick 9000 in 30.08 seconds; replay and save produced; no fatal error or desync.
- One of 16 planned ring cells was impassable water; the remaining 15 began temporarily blocked.
- Retries 1/8 through 5/8 occurred at ticks 1 through 5. Product release was `complete` at tick 821 with 5/8 retries.
- The observer timestamped 15 walls plus the terrain seal complete at tick 825, or 20.625 seconds. No Silo or defense existed first.
- Later timestamps: Silo 4825, configured first defense 6270, normal secondary construction 6295. Exact order passed: `825 < 4825 < 6270 < 6295`.
- Narration: `.build/20260820-cnc101-enclosure-retry/analysis/game1-narrator/NARRATIVE.md`.
- Policy review: `.build/20260820-cnc101-enclosure-retry/analysis/game1-policy/POLICY-REVIEW.md` — bounded pass; required follow-up for a materially different layout.
- Disposition: exercised directly in Game 2's clear full ring, later blocker timing, reversed target faction, and nearby enemy pressure.

### Game 2: clear ring under pressure

- Artifact root: `.build/20260820-cnc101-enclosure-retry/game2-qualified-final/cnc101-game2-control`
- Ordinary all-module AIs: target Brutalis/Nod versus VIKI/GDI; headless MAX; seed 1018202; nearby enemy infantry pressure.
- Exit 0 at tick 9000 in 32.057 seconds; replay and save produced; no fatal error or desync.
- All 16 ring cells were clear terrain and temporarily blocked through tick 6.
- Retries 1/8 through 7/8 occurred at ticks 1 through 7. Product release was `complete` at tick 296 with 7/8 retries.
- The observer timestamped all 16 walls complete at tick 300, or 7.5 seconds. No Silo or defense existed first.
- Later timestamps: Silo 5520, configured first defense 6705, normal secondary construction 7575. Exact order passed: `300 < 5520 < 6705 < 7575`.
- Narration: `.build/20260820-cnc101-enclosure-retry/analysis/game2-narrator/NARRATIVE.md`.
- Policy review: `.build/20260820-cnc101-enclosure-retry/analysis/game2-policy/POLICY-REVIEW.md` — bounded pass; Game 1 follow-up resolved; optional extra geometry coverage advisory only.
- Disposition: record the bounded two-geometry claim here. Reject an extra game in this cycle because acceptance explicitly requires exactly two qualifying games and both requested adversarial cases passed.

## Remaining risk

The two games cover one terrain-sealed hole and one clear full ring with distinct retry timing, faction, and pressure. They do not prove every map geometry. The retry-limit escape is deterministically unit-tested but was intentionally not triggered in the qualifying games because both games were required to complete the enclosure before normal secondary construction.
