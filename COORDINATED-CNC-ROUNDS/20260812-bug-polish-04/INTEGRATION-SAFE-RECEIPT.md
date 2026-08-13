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

## Integration cycle 2 complete

- Candidate tested: `3e89d5ad5b3f8df5f3e819aeb15266573c2a03e4`.
- Exactly two valid focused full-engine games completed after two world-tick-0
  Lua fixture failures were repaired without consuming the cycle.
- The attributed crate/husk game passed: unique `e1` assignment and exact-cell
  collection were proven, construction restoration gated the second crate and
  region exploration, and concurrent CNC-97 ORCA-husk recovery completed.
- The wall game proved safe non-placement but not reconstruction: its authored
  route crossed the Fact footprint, leaving the legal target `routed=0` after
  unblock. This is an integration-fixture error and inconclusive CNC-107 evidence,
  not a concrete source defect.
- Separate fresh Luna narration and policy review completed for both games.
- No source repair or balance change was made; combined checks passed.
- Durable result: integration is **2 / 5**. Cycle 3 should reuse CNC-107's proven
  task fixture/geometry. Publication, push, PR update, and merge authority remain
  absent.

## Integration cycle 3 complete

- Candidate tested: `1ace5799d507db0ec9b5103ec95c9714930b4036`.
- Exactly two focused full-engine games used byte-identical copies of CNC-107's
  proven cycle-5 connected and hostile fixtures, their canonical launcher setup,
  ordinary Skynet/Brutalis AIs with all modules, and installed CNC content.
- The connected asymmetric-origin game passed at tick 7600 (15.019 seconds),
  proving the `(27,31)` route, wall confirmation, and final-under-pressure
  inventory. The hostile sole-route game passed at tick 7600 (19.026 seconds),
  proving the corner-first probes and placement at `(28,19)`, bounded no-route
  terminal, and save creation. Both exited 0 without fatal/desync signals.
- Separate fresh Luna narration and policy review completed for both games. No
  CNC-97/98 assertion was added because preserving the exact proven wall fixture
  took precedence.
- The cycle-3 Luna code review was clear. Its one advisory evidence-risk finding
  (matched control and broader contention remain untested) was dispositioned to
  later planned cycles; it requested no repair.
- No source repair or balance change was made; combined diff, Debug/interface,
  and Release test checks passed.
- Durable result: integration is **3 / 5**. Proceed to cycle 4. Publication,
  push, PR update, and merge authority remain absent.
