# CNC-96A Air-verbatim integration cycle 3

- Status: complete; clean committed candidate pending coordinator review
- Base: `0f807a81cf8e9be1b8f6b4c3abd7ad4314223fea`
- Branch: `agent/20260824-cnc96a-air-verbatim-cycle3`
- Worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260824-cnc96a-air-verbatim/integration3`
- Ownership: original `SquadManagerBotModule` is the sole `List<Squad>` owner; specialist groups are ordinary `SquadType.Stealth` squads
- Profiles: `stealth-tank`/STNK and `chemical`/CTNK, shared implementation, aggregate maximum four squads
- Matrix: 98/98 restored live mappings, 51 copied-Air authorities, 35 retreat exclusions, five retreat-free extractions
- Product validation: solution build 0 warnings/0 errors; CNC YAML pass; focused provenance tests 6/6; Air-copy checker pass
- Final games: 2/2 passed at tick 3000 under `/tmp/cnc96a-cycle3-games-final2`
- Final reviews: fresh Luna narrative and serialized policy PASS for each game; no policy action
- Raw logs, maps, benchmarks, and replays remain ignored/outside Git
- No push or PR
