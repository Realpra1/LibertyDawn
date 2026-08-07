# Integration assignment: 20260807-bug-polish-02

Read applicable `AGENTS.md` and the repository integration role skill. This file
is the complete integration contract. Do not read the task sheet, coordinator
state, worker specs/reports, or conversation history.

## Exact release inputs

- Common base: `419bee2531d4802bf922c3597b42c6eeb75ab250`
- Stable release branch to create: `agent/cnc-20260807-bug-polish-02-release`
- Intended release PR base: `bleed`
- Worker 1 / CNC-87 / PR #86 / reviewed head:
  `5170183fb882ccf68d1970052269e11c4d739ead`
- Worker 2 / CNC-40 / PR #87 / reviewed head:
  `40ed5926864c564cf801dff0cd4cb7da183bbeb7`
- Worker 3 / CNC-41 / PR #88 / reviewed head:
  `418786381f64b1cae4ff9a8d1d943c78d5666646`
- Worker 4 / CNC-42 / PR #89 / reviewed head:
  `260d10e9654c582f3b187d90d6d280195e896ede`
- Worker 5 / CNC-44 / PR #85 / reviewed head:
  `df9cd6e12fd5ab55e7d40d2e421bf4d83135945d`
- Every individual task PR's required Linux/Windows checks passed at its final
  head. Source PRs remain open and must not be merged through GitHub.

## Required integration

1. Verify this worktree is detached at the exact common base, fetch without
   discarding anything, create the stable release branch, and locally merge all
   five reviewed heads with merge commits. Preserve the individual commit
   histories and record any conflict and its resolution here.
2. Do not merge, push, or commit directly to `bleed`. Do not use GitHub's merge
   action on source PRs. Do not include the coordinator's unrelated working
   branch or any unreviewed task head.
3. Balance is frozen. Merge compatibility work must not change costs, HP, damage,
   armor, speed, timing, power, prerequisites, probabilities, resource values, or
   similar tuning. Do not manufacture improved AI results through balance.
4. Run proportionate combined compile/interface/CNC rules gates under the shared
   large-build lock at
   `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`.
   Keep raw build and game artifacts out of Git.
5. Push the stable release branch and open one draft release PR to `bleed`. This
   first combined head is RC1. Record its exact head, included merge heads, gates,
   PR URL, conflicts, and repair routing in this file.

## Integrated-test routing

- Do not silently repair task behavior during the merge. After RC1 exists, the
  coordinator will reactivate task-scoped workers on repair branches based on the
  exact RC head, up to three code-change cycles per worker for this candidate.
- CNC-42 is `First iteration - testing`. Its first integrated repair should
  preserve exact economy-SAM placement ownership across a save made after build
  reservation but before placement, then run the reset clean-three, stressed
  final regression, and combined CNC-41 G4/G5/G7 validation.
- CNC-41 is `First iteration - testing`; retain its recorded route-proof gap for
  integrated validation rather than inventing acceptance during merge.
- CNC-40 and CNC-44 are also `First iteration - testing`; preserve their stated
  limitations for task-scoped combined validation.
- User acceptance clarification: save/load and replays must remain correct and
  desync-free with sensible restored AI state, but a loaded match need not repeat
  an uninterrupted match's exact actor decisions or ticks unless a narrow
  task-specific persisted invariant requires it.

Return only the release branch/head, draft PR URL, gate result, conflicts, and
which workers must be reactivated for RC1.

## Receipt publication follow-up

RC1 assembly and the draft PR are complete. Commit and push this integration
receipt as the only follow-up change on the stable release branch. Do not alter
product code, merge another head, change the recorded RC1 product SHA, merge a
PR, or write to `bleed`. Report the documentation-only final head and PR checks.

## RC1 integration receipt

- Stable release branch: `agent/cnc-20260807-bug-polish-02-release`
- Common base: `419bee2531d4802bf922c3597b42c6eeb75ab250`
- RC1 head: `394ae5eeadfffbf58a9db7c1fac91960f5158cb6`
- Draft release PR to `bleed`: https://github.com/Realpra1/LibertyDawn/pull/90
- Source PRs #85-#89 remain open and were not merged through GitHub.

### Included reviewed heads and merge order

1. Worker 1 / CNC-87 / PR #86 / `5170183fb882ccf68d1970052269e11c4d739ead`
   merged as `cd403a7779`.
2. Worker 2 / CNC-40 / PR #87 / `40ed5926864c564cf801dff0cd4cb7da183bbeb7`
   merged as `d78e344279`.
3. Worker 3 / CNC-41 / PR #88 / `418786381f64b1cae4ff9a8d1d943c78d5666646`
   merged as `3556a62b37`.
4. Worker 4 / CNC-42 / PR #89 / `260d10e9654c582f3b187d90d6d280195e896ede`
   merged as `3a95e7ee9d`.
5. Worker 5 / CNC-44 / PR #85 / `df9cd6e12fd5ab55e7d40d2e421bf4d83135945d`
   merged as `394ae5eead`.

Every supplied head was verified to descend from the common base, match its open
source PR head, and have successful Linux and Windows required checks. The RC1
non-merge history is exactly the union of the five supplied reviewed histories;
all five supplied heads are ancestors of RC1.

### Conflicts and aggregate inspection

- Merge conflicts: none. Git's `ort` strategy merged all five heads cleanly in
  the required order, including overlapping base-builder and CNC AI rules files.
- Manual conflict resolutions: none.
- Balance compatibility edits by the integrator: none.
- `git diff --check 419bee2531d4802bf922c3597b42c6eeb75ab250 HEAD`
  passed. Aggregate changed-file and overlapping-file inspection found no extra
  unreviewed history or merge-marker/whitespace damage.

### Combined gates

The following CI-equivalent sequence ran successfully while holding the
`integrator` large-build reservation in
`/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260807-bug-polish-02/locks`:

```sh
make check && make check-scripts && make test && dotnet test OpenRA.Test/OpenRA.Test.csproj
```

- Debug compile and explicit/conditional interface checks: passed, zero build
  warnings and zero errors.
- Script/Lua checks: passed.
- Release compile and CNC MiniYAML/default-sequence/map validation: passed, zero
  build warnings and zero errors.
- C# unit suite: passed, 512 passed / 0 failed / 0 skipped. Compilation of the
  test project emitted one non-blocking CA1825 warning in the reviewed CNC-44
  `AircraftHuskSpawnEligibilityTest.cs`; the test run remained clean.

### RC1 repair and validation routing

- Reactivate Worker 2 / CNC-40 for its task-scoped combined validation and stated
  first-iteration limitations.
- Reactivate Worker 3 / CNC-41 for combined validation of its retained route-proof
  gap.
- Reactivate Worker 4 / CNC-42 first for exact economy-SAM placement ownership
  across a save after build reservation but before placement, then its reset
  clean-three, stressed final regression, and combined CNC-41 G4/G5/G7 validation.
- Reactivate Worker 5 / CNC-44 for its task-scoped combined validation and stated
  first-iteration limitations.
- Worker 1 / CNC-87 does not require RC1 reactivation unless a later repair touches
  coordinated role-launch or resource-slot behavior.

RC1 remains draft pending the routed combined full-engine/adversarial validation,
matched controls where required, and any task-scoped repair candidates.
