# CNC-44 Task Report — Aircraft husks

## Status

`First iteration - testing`. Cycle 1 is implemented; its matched empty-cell control/changed pair passes and a harder type/capture batch establishes the scoped behavior below. CNC-62 remains unavailable and publication-blocking, so this is not a complete handoff.

## Behavior and design

- `SpawnActorOnDeath` has an explicit default-off `RequiresValidHuskCell` mode.
- The mode validates the configured target as a `Husk`, checks the target actor's standard `Husk.AllowedTerrain`, and inventories every exact-cell ActorMap influence at the final frame-end creation boundary.
- Dead actors do not block; any live influence blocks. Simultaneous attempts revalidate at creation, so the first created full-cell husk blocks later attempts.
- CNC rules opt in only `TRAN.Husk`, `HELI.Husk`, and `ORCA.Husk`, each targeting a distinct standard `^AircraftGroundHusk` actor with exact `TransformOnCapture` mapping. A10 and C17 are unchanged.
- Diagnostics are one bounded debug line per opted-in transition and include source/created actor IDs, exact cell, result, and rejection reason/blockers.

No crash damage, HP, armor, speed, timing, cost, production, AI priority, standard husk durability/decay, or restored-health policy was changed.

## Cycle 1 evidence

Old-control product SHA: `419bee2531d4802bf922c3597b42c6eeb75ab250` (worker pre-product HEAD `abc1011771`). Map SHA-256: `72e5c7d3fad5bcc5aeda991a3a9dfc919d1418674e73d77cac9f4b2e723b43ac`. Seed: 44001. Bots: ordinary SkyNet GDI and Brutalis Nod at headless MAX.

- Valid control: `analysis/worker-5-cnc44/baseline-control/run-004/`; temporary ORCA and A10 fall actors cleared, with zero live/restored aircraft and no durable spawn through tick 90.
- Valid changed: `analysis/worker-5-cnc44/cycle-01/run-001/`; `orca.husk#147` produced exactly one `orca.groundhusk#150` at clear cell 38,34 after removal. A10 stayed transient-only. Clean tick-90 exit with replay and benchmark artifacts.
- Invalid but useful control runs 001/002 exposed and corrected an A10 `AttackBomber` targetless harness crash and a premature natural-game-over test window; neither is acceptance evidence.
- Factual narratives: `analysis/worker-5-cnc44/commentary/control-batch-01/NARRATIVE.md` and `analysis/worker-5-cnc44/commentary/cycle-01-pair/NARRATIVE.md`. Routine policy review is not applicable.

Checks: Release build passed with zero warnings; `make test` passed CNC MiniYAML and map validation; focused `dotnet vstest` ran 5/5 eligibility cases successfully; publication `make check` passed its Debug build and interface checks with zero warnings/errors.

### Type, terrain, capture, and ordinary-module evidence

Seed 44002 used ordinary SkyNet GDI and Brutalis Nod AIs at headless MAX. The final harness SHA-256 was `0f34e7e4048102fb72a70e06aa89259c58c6c5173451982bb17f9f9e6f5832a9`.

- Run 002 safely rejected an ORCA durable husk on Rock terrain while still creating TRAN and HELI ground husks. This run was invalid for capture because the ORCA setup contradicted the standard `Husk.AllowedTerrain` contract.
- Runs 003/004 created exactly one mapped `TRAN.GroundHusk`, `HELI.GroundHusk`, and `ORCA.GroundHusk`, with zero A10 ground/transient remainder after its fall.
- Three ordinary E6 capture orders consumed all three Engineers, removed all three ground husks, and restored exact `TRAN`, `HELI`, and `ORCA` actors under Multi0 at the inherited 25% health policy: 2250/9000, 3750/15000, and 2125/8500.
- All three restored aircraft remained alive and accepted movement; the Apache and Orca were redirected by normal air-squad modules before the delayed assertion, providing module-adoption evidence but making their capture-time facing unverifiable. TRAN facing was verified. This batch is therefore useful evidence, not literal acceptance.
- All three runs reached tick 425 with exit code 0 and no fatal Lua/desync signal. Factual narrative: `analysis/worker-5-cnc44/commentary/cycle-01-type-capture/NARRATIVE.md`.

## Dependency and remaining work

CNC-62 has no local branch, remote branch, or GitHub PR as of both the pre-implementation and pre-publication checks on 2026-08-07. Exact-cell crash damage and combined damage-before-eligibility evidence are therefore not yet available. CNC-40 has a remote branch but no PR, so the contract did not authorize treating it as a publication dependency. Capture-time HELI/ORCA facing, the full occupant matrix, same-frame contention, complete terrain/boundary matrix, save/load, adversarial capture invalidation, stress/natural endurance, final regression, and combined CNC-62 reruns remain. The scoped result must be published as `First iteration - testing`.

## Publication and review

- Product commit: `396c8106d9cec1c84ed0c2e44cd34ce0d0ef4772` on `agent/round-20260807-cnc44-aircraft-husks`.
- PR: `#85`, https://github.com/Realpra1/LibertyDawn/pull/85, targeting `agent/cnc-20260806-bug-polish-01-release`; it remains open and unmerged.
- GitHub checks: Linux and Windows .NET 6.0 passed at the product head. The final handoff-metadata-only successor is rechecked before return.
- Independent Sol-high final review: `analysis/worker-5-cnc44/final-review/REVIEW.md`; verdict `ready`, required fix `none`. The reviewer found no scoped correctness, regression, determinism, performance, scope, or diagnostic defect, while retaining the same CNC-62 and acceptance blockers.

The implementation is event-driven and bounded by actors influencing one exact cell. Ordered blocker diagnostics and all-actor evaluation make the decision deterministic; creation at the actual frame-end boundary makes an earlier same-frame created husk visible to a later attempt. Short matched evidence does not establish a stress/endurance performance conclusion, which remains open with the final portfolio.

## Deferred work

None. The targetless scripted A10 `AttackBomber` exception was a harness misuse and was avoided by instantiating its actual transient actor; no product change is proposed.
