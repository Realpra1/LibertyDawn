# Worker State: CNC-101

## Assignment

- Worker: `WORKER-1`; task: `CNC-101`
- Worker branch to be created by the coordinator: `agent/20260820-cnc101-enclosure-retry-worker-1`
- Worker worktree to be created by the coordinator: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260820-cnc101-enclosure-retry/worker-1-cnc101`
- Exact base: `54f84f580461123dfca9a3cdfe8cf9a62d90188d` (`origin/bleed`)
- Balance is frozen; no architecture, unrelated behavior, or tuning changes are authorized.

## Literal acceptance

The initial MCV enclosure phase must retry missing or terrain-constrained wall placements up to eight times before allowing normal secondary-queue construction. In exactly two distinct ordinary-AI/all-module custom games, each capped at 120 seconds, the enclosure must complete in about 30 seconds or less, including a terrain-constrained-hole game. Each game must directly timestamp enclosure completion and prove that Silo, first configured defense, and other normal secondary construction do not preempt the initial retry phase. After enclosure completion, the observable order must remain Silo, first configured defense, then normal construction. Retries must be bounded and must not create duplicate/unbounded orders.

## Required checks and evidence

- Focused enclosure/wall-planner tests covering missing placements, terrain holes, eight-retry bounds, no duplicate orders, and post-enclosure queue release.
- Protected repository checks, CNC YAML validation, scenario/map validation, and `git diff --check`.
- Exactly two fresh full-engine games only: ordinary AI with all modules enabled, one terrain-constrained-hole adversarial map and one distinct control/adversarial map. Preserve concise timestamps, retry/queue observations, crashes/desyncs, and paths in the task report; keep raw artifacts out of Git.
