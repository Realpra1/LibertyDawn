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

## Cycle 2: Terra correction and no-cap amendment

### Proposal

`Complete - testing`

Starting from clean cycle-1 commit `47979e1dfac5d6900dc80e7d983a4e3e965970a6`, Terra found that the configured cutoff stopped unresolved initial-enclosure maintenance and made the secondary gate false. Terra also identified a wall-cap early return. The user amended CNC-101 to remove the cap itself. Authorized Task Maker commit `d2c4640327` updated only the task entry and worker acceptance; it was incorporated with `cherry-pick --no-commit` so cycle 2 still has one worker commit.

The final correction keeps an unresolved bound enclosure active across cutoff, classifies cutoff as unavailable maintenance, and increments the persisted retry count until explicit 8/8 release. Physical/terrain closure is still checked first. The `MaximumWallSegments` field and description, all count/early-return planner logic and obsolete parameters, and all nine CNC AI YAML values (eight at 24 and Skynet at 150) are removed. There is no global or per-player wall-count ceiling in this policy.

### Cycle-2 checks

- Focused enclosure/opening tests: **46 passed, 0 failed**; four unrelated pre-existing analyzer warnings appeared in other test files.
- Protected canonical wrapper plus `make check`: **passed**, build **0 warnings, 0 errors**, interface checks passed.
- Full `./utility.sh cnc --check-yaml`: **passed** after all no-cap configuration changes.
- Cutoff and replacement no-cap custom-map YAML plus embedded Lua: **passed**.
- Production/config search found no `MaximumWallSegments` reference under `OpenRA.Mods.Common`, `OpenRA.Test`, or `mods/cnc`.
- `git diff --check`: **passed**.

### Cycle-2 qualifying Game 1: cutoff remains gated

- Artifact: `.build/20260820-cnc101-enclosure-retry/cycle2-game1-final/cnc101-cycle2-game1-cutoff`.
- Ordinary all-module Brutalis/GDI versus VIKI/Nod on the final no-cap code and a map with no cap setting; all 16 target perimeter cells blocked; cutoff tick 4; one-tick focused maintenance.
- Initial unavailable observations reached 3/8 before cutoff. Cutoff maintenance remained pending at ticks 4–7 with retries 4/8–7/8, then explicitly released at tick 8 for `retry limit reached`, 8/8. No Silo or defense appeared before release.
- Later order: Silo 4577, configured defense 6032, normal secondary 6059. Exit 0 at tick 9000 in 40.035 seconds; replay/save present; no fatal error or desync.
- Narration: `.build/20260820-cnc101-enclosure-retry/analysis/cycle2-game1-narrator/NARRATIVE.md`.
- Policy: `.build/20260820-cnc101-enclosure-retry/analysis/cycle2-game1-policy/POLICY-REVIEW.md` — bounded pass; optional other cutoff timings advisory.
- Disposition: retain the all-blocked cutoff scope; no extra cutoff game because the exact-two cycle's second game is required to prove the independent no-cap amendment.

### Cycle-2 qualifying replacement Game 2: construction exceeds former cap

- Artifact: `.build/20260820-cnc101-enclosure-retry/cycle2-game2-qualified/cnc101-cycle2-game2-exceed`.
- Ordinary all-module Brutalis/Nod versus VIKI/GDI under nearby infantry pressure; 24 prior owned walls, one impassable terrain-sealed ring cell, and 15 temporarily blocked buildable ring cells.
- Retries 1/8–6/8 occurred at ticks 1–6; blockers cleared tick 5. The first new enclosure wall raised total ownership from the former 24-wall setting to 25 at tick 80.
- Physical enclosure completed at tick 656 with 39 total walls (24 prior + 15 new), 16.4 seconds, and product release `complete` at retries 6/8. No Silo or defense appeared first.
- Later order: Silo 5600, configured defense 6728, normal secondary 8114. Exit 0 at tick 9000 in 35.081 seconds; replay/save present; no retry-limit release, fatal error, or desync.
- Narration: `.build/20260820-cnc101-enclosure-retry/analysis/cycle2-game2-narrator/NARRATIVE.md`.
- Policy: `.build/20260820-cnc101-enclosure-retry/analysis/cycle2-game2-policy/POLICY-REVIEW.md` — bounded above-cap pass; optional broader counts/layouts advisory.
- Disposition: retain the directly tested 24-to-39, terrain-sealed pressure scope; no extra game is required in this exact-two cycle.

### Excluded setup/calibration runs

- The original cutoff qualification ran before the no-cap amendment and used a scenario copy containing the former 24-wall setting. Its cutoff result was valid for the earlier patch, but it is superseded and excluded so both final qualifying games exercise the final no-cap code/configuration.
- The cap-specific Game 2 was interrupted at tick 5000 immediately when the user removed the wall-cap policy. It is uncounted and not acceptance evidence.
- The first no-cap replacement launch reached tick 9000 and proved construction from 24 to 32 walls, but a scenario-only one-tick maintenance override exhausted retries before closure. It is uncounted. Restoring the ordinary 250-tick maintenance interval produced the qualifying replacement above; product code did not change between these launches.

### Remaining risk

The evidence covers an impossible all-blocked cutoff and one terrain-sealed 24-to-39 wall expansion under pressure. It does not prove every starting count, topology, or cutoff timing. Both Luna policy reviewers classified broader coverage as advisory, not a blocker or required follow-up.

## Cycle 3: Terra maintenance-aging correction

### Proposal

`Complete - testing`

Starting from clean cycle-2 commit `9ca432f4d06e8b83b7e5dfb915ad80e572984aee`, Terra found that ordinary unavailable-cell and cutoff-unavailable retries were still aged by competing one-tick queue polls rather than the configured enclosure maintenance interval. `NextEnclosureScanTick` deliberately kept the protected secondary queue responsive, but the no-legal and cutoff branches incremented on every resulting scan.

The smallest correction adds a persisted `NextInitialRetryTick` shared by unavailable-cell, cutoff-unavailable, and due issued-cell retry outcomes. A retry can be consumed only when that timestamp is due, then the timestamp advances by at least one configured maintenance interval. Responsive queue polling remains intact without accelerating the maximum-eight escape. Save data advances from version 4 to version 5; versions 2-4 remain accepted with immediate eligibility, future absolute retry ticks remain valid for deterministic restoration, negative scheduled ticks are rejected, and invalid state retains the existing safe-disable behavior. No-wall-cap behavior, per-Fact physical/terrain closure, cutoff gating, and later queue order are unchanged.

### Cycle-3 checks

- Focused enclosure/opening tests: **50 passed, 0 failed**. The new interval-250 regression simulates four competing polls per tick and consumes only at ticks 1 and 251, with the next retry at 501. Four unrelated pre-existing analyzer warnings appeared in other test files.
- Protected canonical large-build wrapper plus `make check`: **passed**, build **0 warnings, 0 errors**, explicit interface checks passed.
- Full `./utility.sh cnc --check-yaml`: **passed**.
- Both cycle-3 custom-map YAML validations: **passed**.
- Both embedded scenario Lua syntax checks: **passed**.
- `git diff --check`: **passed**.

### Cycle-3 qualifying Game 1: recoverable blockers and terrain seal

- Artifact: `.build/20260820-cnc101-enclosure-retry/cycle3-game1/cnc101-cycle3-game1-recover`.
- Ordinary all-module Brutalis/Nod versus VIKI/GDI under nearby infantry pressure; normal maintenance interval 250; 24 prior owned walls, fifteen temporarily blocked ring cells, and one impassable terrain-sealed ring position.
- Retry 1/8 occurred at tick 1 with `next-retry=251`. Blockers cleared at tick 5; competing polling produced no retry 2. The first target ring-wall plan occurred at tick 251.
- Total ownership exceeded the former cap at 25 walls on tick 323. Physical/terrain closure completed at tick 902 with 39 total walls, 22.55 seconds at 40 ticks/second, and product release `complete` at retries 1/8. No ordinary secondary acceptance marker preceded release.
- Later order: Silo 5546, configured defense 7265, normal secondary 7439. Exit 0 at tick 9000 in 36.088 seconds; replay/save present; no scenario failure, retry-limit release, fatal error, or desync.
- Narration: `.build/20260820-cnc101-enclosure-retry/analysis/cycle3-game1-narrator/NARRATIVE.md`.
- Policy: `.build/20260820-cnc101-enclosure-retry/analysis/cycle3-game1-policy/POLICY-REVIEW.md` — highest-priority verdict `KEEP`.
- Disposition: accept `KEEP` without code or policy changes; retain the review's bounded one-scenario claim and use the required distinct cutoff game for the impossible-geometry path.

### Cycle-3 qualifying Game 2: permanent blockage and cutoff

- Artifact: `.build/20260820-cnc101-enclosure-retry/cycle3-game2/cnc101-cycle3-game2-cutoff`.
- Ordinary all-module Brutalis/GDI versus VIKI/Nod; normal maintenance interval 250; all sixteen ring positions permanently unavailable; cutoff tick 4.
- Target retries occurred exactly at ticks 1, 251, 501, 751, 1001, 1251, 1501, and 1751. Cutoff therefore did not bypass the gate or accelerate the retries under competing polling.
- Physical closure was deliberately impossible. Product release occurred only at explicit `retry limit reached`, 8/8, tick 1751. The scenario's tick-1751 secondary floor detected no preemption.
- Later order: Silo 4898, configured defense 6374, normal secondary 6407. Exit 0 at tick 9000 in 34.127 seconds; replay/save present; no physical-complete release, scenario failure, fatal error, or desync.
- Narration: `.build/20260820-cnc101-enclosure-retry/analysis/cycle3-game2-narrator/NARRATIVE.md`.
- Policy: `.build/20260820-cnc101-enclosure-retry/analysis/cycle3-game2-policy/POLICY-REVIEW.md` — highest-priority verdict `KEEP`.
- Disposition: accept `KEEP` without code or policy changes; retain the bounded permanent-blockage/cutoff claim. Exactly two qualifying games are complete, so no extra game is added.

### Excluded setup attempt

The first Game-2 command used the launcher's unsupported `--output-dir` option. The launcher exited 2 before engine startup and acquired no game evidence. It is disclosed and uncounted. The corrected `--output` launch used the unchanged manifest/map and is the qualifying Game 2 above.

### Remaining risk

The games cover recovery after transient blockers with a terrain seal, construction beyond the former cap, and maintenance-aged explicit exhaustion under permanent blockage plus cutoff. They do not cover every geometry or a mid-run save reload. Save/load determinism is supported by the versioned absolute timestamp, compatibility validation, and focused policy tests; both Luna policy reviewers classified the observed behavior `KEEP` within the staged evidence bounds.
