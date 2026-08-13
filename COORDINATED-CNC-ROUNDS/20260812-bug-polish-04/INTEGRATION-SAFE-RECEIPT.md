# Round 04 Safe Integration Receipt

- Candidate branch: `agent/cnc-20260812-bug-polish-04-release`.
- Base: `06c243f3c329f52ba3216725b0bb21f8fc763030` (`origin/bleed`).
- Source PR heads merged locally, in order: CNC-97 / #101
  `22144d24e2150cec756e71d858aa39e865e32a8f`; CNC-98 / #102
  `48a33afb13171e627b6dc17a036df9b268a4c197`; CNC-107 / #103
  `6d2c1faff337fc74fbbb7054975e9afa88976976`.
- All twelve reported Linux/Windows source-PR checks were successful and all
  source PRs were open and CLEAN at verification.
- Local integration merges were clean. Combined `git diff --check`, `make check`,
  and Release `OpenRA.Test` passed.
- CNC-96 and CNC-100 were not merged as source PRs because their receipts marked
  them unsafe / `First iteration - testing`. CNC-100 is nevertheless inherited
  from the already-advanced `bleed` base; this is a release risk, not approval.
- No game was launched, no integration cycle was consumed, no branch was pushed,
  and no PR was created or updated: publication authority was not granted.
- Continue from [INTEGRATION-SAFE-STATE.md](INTEGRATION-SAFE-STATE.md) for at
  most five Sol-medium focused integration cycles.

## Integration cycle 1 complete

- Candidate tested: `41bbdb4d6ce4999a5193d86d6d9cc72497722211`.
- Exactly two distinct focused full-engine games completed with headless MAX,
  ordinary enemy AIs, isolated support directories, and installed CNC content.
- Game 1 passed all CNC-97 aircraft-husk recovery and CNC-98 VIKI gate assertions
  at tick 2800.
- Game 2 reached tick 10000 cleanly and demonstrated CNC-107 exact-route wall
  deferral, legal individual placement, and preserved two-way access under enemy
  construction pressure. Its preselected repair cell was never built and the
  late crate removal lacked cause attribution, so neither is a concrete release
  defect.
- Separate fresh Luna narration and policy review completed for both games.
- No source repair or balance change was made; combined checks passed.
- Durable result: integration is **1 / 5**; continue with cycle 2. Publication,
  push, PR update, and merge authority remain absent.
