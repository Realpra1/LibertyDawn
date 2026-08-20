# Worker State: CNC-101

## Assignment

- Worker: `WORKER-1`; task: `CNC-101`
- Worker branch supplied by the coordinator: `agent/20260820-cnc101-enclosure-retry`
- Worker worktree supplied by the coordinator: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260820-cnc101-enclosure-retry/worker`
- Exact base: `54f84f580461123dfca9a3cdfe8cf9a62d90188d` (`origin/bleed`)
- Balance is frozen; no architecture, unrelated behavior, or tuning changes are authorized.

## Literal acceptance

The initial MCV enclosure phase must retry missing or terrain-constrained wall placements up to eight times before allowing normal secondary-queue construction. In exactly two distinct ordinary-AI/all-module custom games, each capped at 120 seconds, the enclosure must complete in about 30 seconds or less, including a terrain-constrained-hole game. Each game must directly timestamp enclosure completion and prove that Silo, first configured defense, and other normal secondary construction do not preempt the initial retry phase. After enclosure completion, the observable order must remain Silo, first configured defense, then normal construction. Retries must be bounded and must not create duplicate/unbounded orders.

## Required checks and evidence

- Focused enclosure/wall-planner tests covering missing placements, terrain holes, eight-retry bounds, no duplicate orders, and post-enclosure queue release.
- Protected repository checks, CNC YAML validation, scenario/map validation, and `git diff --check`.
- Exactly two fresh full-engine games only: ordinary AI with all modules enabled, one terrain-constrained-hole adversarial map and one distinct control/adversarial map. Preserve concise timestamps, retry/queue observations, crashes/desyncs, and paths in the task report; keep raw artifacts out of Git.

## Cycle-1 receipt

- Status: `Complete - testing`.
- Root cause: secondary-queue release was coupled to a global four-wall count instead of the bound first Construction Yard's complete 16-cell enclosure, and there was no persisted bounded initial-retry phase. Competing queue polls also needed to avoid consuming retries while a placement confirmation was still in flight.
- Change: the first-yard enclosure now remains the secondary-queue gate until every perimeter cell is closed by an owned wall or impassable terrain, or until a persisted retry counter saturates at eight. Issued-cell retries occur only at the maintenance interval. Save state is versioned compatibly, and the existing post-release Silo/configured-defense/normal order is retained.
- Focused tests: 45 passed, 0 failed. Protected `make check`: passed with 0 warnings and 0 errors. Full CNC YAML validation, both custom-map validations, and `git diff --check`: passed.
- Qualifying Game 1, terrain hole: Brutalis GDI versus VIKI Nod, 30.08 seconds, tick 9000, retries 5/8, product release tick 821, 15 walls plus one water-sealed perimeter cell complete tick 825 (20.625 seconds), then Silo/configured defense/normal secondary at ticks 4825/6270/6295. No failure, retry-limit release, desync, or fatal error.
- Qualifying Game 2, clear pressured control: Brutalis Nod versus VIKI GDI, 32.057 seconds, tick 9000, retries 7/8, product release tick 296, all 16 walls complete tick 300 (7.5 seconds), then Silo/configured defense/normal secondary at ticks 5520/6705/7575. No failure, retry-limit release, desync, or fatal error.
- Separate Luna narration and policy review completed for each qualifying game. Game 1's required follow-up for a materially different layout was exercised by Game 2 and resolved. Game 2's remaining recommendation is advisory only: keep conclusions bounded to the two tested geometries; no additional game or code change is required in this exact-two cycle.
- Detailed receipt and ignored artifact paths: `REPORT.md`.
