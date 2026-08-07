# Integration: coordinated bug/polish RC1 preview

- Round: `20260806-bug-polish-01`
- Status: partial RC1 preview; draft release PR open
- Recorded common product base: `09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf`
- Stable branch: `agent/cnc-20260806-bug-polish-01-release`
- Exact combined code candidate: `545b857af809450ecf4d21c76b4a6884d3a9297e`
- Draft release PR: https://github.com/Realpra1/LibertyDawn/pull/82
- Release target: `bleed`

## Source verification

All supplied heads descend from the recorded common product base. GitHub reported
each source PR open, clean, and mergeable immediately before integration. Source
PRs remain open and were not merged through GitHub.

| Task | Source PR | Integrated head | Receipt / response | GitHub checks at integration |
| --- | --- | --- | --- | --- |
| CNC-39A | #80 | `937ef0204870ff2eca39c413af7431adb279c082` | `REVIEW-2.md` reviewed `464dd7ad7b76a8833927eff4c415e046d43c9ef2`; `f3fbbb4da48a66739bfc7195a3f3b4f91e5e3d16` implements the required save-restoration/finality correction and the supplied head records the review response | Linux .NET 6.0 passed; Windows .NET 6.0 passed |
| CNC-43 | #78 | `b229612791fe82f2c08e5225325e8c707d69f92f` | `REVIEW-3.md` reviewed `52250bb084ca804856d1bac0f0f59a73a4842ddd`; supplied head records the requested long-pressure evidence | no configured check rollup |
| CNC-43A | #79 | `ade3f9d3254d57de117a252b0d7537f306e5c3ae` | `REVIEW-4.md` reviewed `f584f56f12915d650bb3739cb39bfd31ee8a373a`; supplied head records the requested natural-match outcome and terminal state | no configured check rollup |

The integration job explicitly selected the supplied post-review response heads
for this preview.

## Merge order

All entries were merged locally with merge commits, in the required order:

1. Coordinator/stable metadata `e0ec7f7c7b404d2c5caabdf7bde8466636ccdc35`
   -> merge commit `ecae89a7db`.
2. CNC-39A `937ef0204870ff2eca39c413af7431adb279c082`
   -> merge commit `402c808aeb`.
3. CNC-43 `b229612791fe82f2c08e5225325e8c707d69f92f`
   -> merge commit `cdaf8f214d`.
4. CNC-43A `ade3f9d3254d57de117a252b0d7537f306e5c3ae`
   -> merge commit and exact combined code candidate `545b857af809450ecf4d21c76b4a6884d3a9297e`.

## Conflicts and combined-diff inspection

- No merge conflicts occurred. `mods/cnc/rules/vehicles.yaml` was touched by both
  CNC-43 and CNC-43A and was combined automatically without losing either task's
  MCV locomotor or Flame Tank HP change.
- The combined product diff was inspected across `OpenRA.Mods.Common`,
  `OpenRA.Test`, and `mods/cnc` for conflict damage, duplicated policy, and noisy
  diagnostics.
- CNC-39A diagnostics remain behind the existing bot-debug controls; no
  unconditional debug output was found.
- `git diff --check 09ccdac3c1ecb5134a4751f2bcbd8a7970dfe6bf..545b857af809450ecf4d21c76b4a6884d3a9297e`
  passed.

## Combined checks

The build-dependent commands ran under the round's capacity-one global build
lock.

- `make check`: passed; Debug build succeeded with 0 warnings and 0 errors;
  explicit-interface and conditional-trait-interface checks passed.
- `dotnet test OpenRA.Test/OpenRA.Test.csproj --configuration Debug --nologo`:
  passed 445/445, with 0 failed and 0 skipped.
- `make check-scripts`: passed CNC/common Lua syntax validation.
- `make test`: passed; Release build succeeded with 0 warnings and 0 errors, then
  CNC MiniYAML, default sequences, and all CNC maps passed validation.

## Exclusions and remaining preview risk

- CNC-39: explicitly excluded because its worker was still running when this
  preview was requested.
- CNC-51: explicitly excluded because its worker was still running when this
  preview was requested.
- This is a partial preview, not the final product candidate. Combined
  full-engine ordinary-AI/adversarial regression and any task-scoped repair rounds
  remain for the later complete release candidate.
