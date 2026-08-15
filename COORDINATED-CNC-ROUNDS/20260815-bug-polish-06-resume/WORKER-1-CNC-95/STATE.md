# Worker State: CNC-95

## Assignment

- Worker: `WORKER-1`
- Task: `CNC-95 — Detect and safely recover interrupted external coordinated workers`
- Status: `Complete - testing; PR117 port cycle 1 complete`
- Base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Task branch: `agent/round-20260815-cnc95-worker-recovery`
- PR base: `4f806e742bd12145d2a601cc9ff71c3a0b141a13`
- Balance: frozen; coordination infrastructure only
- Cycle: `1/20`
- PR: none

## Literal scope

Detect genuinely interrupted external workers and recover them safely. The
implementation must:

1. Validate stable worker/supervisor identity and meaningful process-tree
   liveness; stale JSON or a raw PID is never sufficient.
2. Atomically transition a disproven `running` attempt to an explicit
   interrupted/blocked state with reason and identity observations.
3. Preserve the exact dirty worktree, durable STATE.md, report, cycle ledger,
   logs, and partial evidence; interrupted game/build evidence is non-acceptance.
4. Relaunch exactly the durable assignment and authorized worktree once, using
   current protected launcher policy, without duplicate workers or cycle changes.
5. Preserve explicit stop/cancel, task-branch/path/role isolation, and CNC-94
   kernel-flock truth. Never steal an unknown lock or signal an unverified PID.

No game balance, AI behavior, strategy, content, map, or lobby change is allowed.

## Minimal acceptance and checks

- Focused process scenarios, not gameplay fixtures, cover stale status, PID
  reuse, worker/supervisor and partial-tree death, dirty-worktree preservation,
  interrupted evidence, concurrent recovery, explicit stop, and one multi-worker
  event. Each scenario is <=120 seconds.
- Every cycle uses two distinct materially useful process scenarios where
  applicable; no irrelevant full-engine games are required.
- Prove one recovery winner, unchanged dirty/state/report/cycle content,
  non-acceptance of partial evidence, unchanged flock inode/ownership, and no
  signal to unrelated processes.
- Run focused launcher/recovery/resource tests, syntax checks, and `git diff
  --check`; do not add broad policy-review or gameplay-performance gates.

## Selective port dependency

The prior task branch is `agent/round-20260814-cnc95-worker-recovery`. Inspect
its commits selectively and port only task-faithful infrastructure changes that
apply cleanly to PR117. Never wholesale-merge its STATE.md, process metadata,
report, evidence, or branch history.

## Handoff

Keep this file and the task report current on the task branch. Do not edit the
task sheet or coordinator state, push `bleed`, or merge a PR.

## Cycle 1 result

- Selectively ported the task-faithful recovery runtime, launcher integration,
  watchdog, explicit stop/start commands, CNC-94 resource-owner safeguards, and
  focused process tests from `agent/round-20260814-cnc95-worker-recovery` onto
  PR117 base `4f806e742bd12145d2a601cc9ff71c3a0b141a13`.
- Excluded the prior branch's worker state, report, process metadata, evidence,
  and unrelated history. No gameplay, balance, AI, content, map, or lobby files
  changed.
- Two materially distinct acceptance scenarios passed: an owned descendant was
  resolved and relaunched exactly once without overwriting durable artifacts;
  and, in a multi-worker event, an unknown reparented lock owner durably blocked
  only its assignment while a peer recovered.
- Focused validation passed: 50 recovery/launcher/resource tests, 8 launcher
  policy tests, Python syntax compilation, and `git diff --check`.
- Interrupted evidence remains non-acceptance, durable stop suppresses automatic
  recovery, identity checks prevent signalling unrelated processes, and kernel
  flock ownership remains authoritative.
- Cycle ledger: cycle 1 consumed; no additional product-change cycle authorized
  or performed in this handoff.
