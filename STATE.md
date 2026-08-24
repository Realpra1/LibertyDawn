# CNC-96A Air-verbatim integration cycle 4

- Status: complete; committed CI correction pending fresh Terra rereview
- Base: `0f807a81cf8e9be1b8f6b4c3abd7ad4314223fea`
- Branch: `agent/20260824-cnc96a-air-verbatim-cycle3`
- Worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260824-cnc96a-air-verbatim/integration3`
- Ownership: original `SquadManagerBotModule` is the sole `List<Squad>` owner; specialist groups are ordinary `SquadType.Stealth` squads
- Profiles: `stealth-tank`/STNK and `chemical`/CTNK, shared implementation, aggregate maximum four squads
- Matrix: 98/98 restored live mappings, 51 copied-Air authorities, 35 retreat exclusions, five retreat-free extractions
- Cycle 4 correction: removed only the unnecessary `using System;` reported by Linux `make check`; no behavior, configuration, or refactor change
- Product validation: exact Linux `make check` and solution build both 0 warnings/0 errors; CNC YAML pass; focused provenance tests 6/6; Air-copy checker pass; diff check pass
- Final games: same two adversarial scenarios passed 2/2 at tick 3000 on final source under `/tmp/cnc96a-cycle4-games-final-rerun`
- Final reviews: fresh separate Luna narratives and serialized fresh Luna policy reviews PASS for each game
- Policy disposition: retain the existing HP/detector regression; record the no-valid-target/route suggestion as advisory and reject scenario expansion for this nonbehavioral CI-only correction
- Raw logs, maps, benchmarks, and replays remain ignored/outside Git
- Draft PR 132 already exists at the prior head; cycle 4 did not push or update it pending Terra READY
