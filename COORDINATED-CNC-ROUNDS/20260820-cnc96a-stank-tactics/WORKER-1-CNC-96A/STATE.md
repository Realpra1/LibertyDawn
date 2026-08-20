# CNC-96A worker state

## Assignment

- Task: amend and implement CNC-96A Stealth Tank ownership and stable tactics.
- Exact base: `d7ac2e346a0505b28d67587b25b28d9f33033ee2` (PR #124 reviewed head).
- Worker branch: `agent/20260820-cnc96a-stank-tactics-worker-1`.
- Worker worktree: `/root/github/LibertyDawn/.worktrees/coordinated-cnc/20260820-cnc96a-stank-tactics/worker-1`.

## Literal acceptance

- Use exactly 6x6 map-cell strategic/coarse cells for Stealth Tank tactics,
  matching Air's coarse-map dimensions; do not change Air behavior.
- Eliminate repeated attack-order cancellation/churn before mission completion.
- Reduce slow reaction beside valid undefended targets; directly timestamp
  target acquisition and attack/reaction latency.
- After attacking or revealing, every Stealth Tank retreats one 6x6 coarse
  strategic cell before reassessment/continuation.
- Preserve every produced/captured Stealth Tank claim, at most four squads per
  AI, one-unit survivor behavior, repair/no-repair/rejoin behavior, and zero
  ordinary-squad leakage. Do not alter unrelated squads, balance, Air, or add
  broad micromanagement.

## Required evidence and checks

- Add focused tests for 6x6 cell geometry, order lifecycle/churn prevention,
  undefended-target reaction, one-cell retreat, ownership/reformation,
  survivor, repair/no-repair/rejoin, four-squad ceiling, and no leakage.
- Run exactly two distinct adversarial ordinary-AI/all-module custom games,
  each under 120 seconds, with separate native Luna factual narration and
  separate native Luna policy review. Each must directly timestamp attack
  order/cancel behavior or stable mission completion, nearby undefended-target
  acquisition/reaction, attack/reveal, and one-cell retreat before the next
  engagement, while recording ownership/reformation/no-leakage and repair/
  rejoin behavior.
- Preserve concise evidence paths; keep raw logs, replays, saves, and build
  output out of Git.
