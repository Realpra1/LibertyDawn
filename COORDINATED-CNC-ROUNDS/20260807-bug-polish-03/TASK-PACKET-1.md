# Task packet
- Task ID: CNC-45
- Title: Economy troop production/use
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base, `468ee64f5a0f9a9e19e260e5c5943e6e878f4705` (`agent/cnc-20260807-bug-polish-02-release`). No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: No active branch or PR matching CNC-45 was found at selection. CNC-43 (MCV crush flavor) is complete and shares the Mammoth crush-capability baseline; it must remain behavior-scoped. CNC-40, CNC-41, CNC-42, and CNC-44 remain excluded prior-round work.
- Cross-worker concern: Preserve the completed Mammoth/MCV capability baseline and do not modify the excluded active-task contracts. This task's Mammoth crush orders must remain bounded and isolated from unrelated economy, harassment, or CPU-performance behavior.

## Authoritative task text

**Economy troop production/use.** Make economy armies primarily Mammoth tanks with riflemen and the artillery squad; use medium tanks, not Mammoths, for harassment. Occasionally give Mammoths bounded crush orders and make their approach distance account for the shortest-ranged usable weapon so cannon damage is not wasted. Preserve ordinary behavior and CPU performance.

## Relevant linked notes

- CNC-43: **MCV crush flavor.** Config only: give MCVs the Mammoth tank's crush capabilities. Do not change AI behavior or unrelated balance.
- CNC-36: **Economy artillery squad.** Create one economy-branch MRLS artillery cluster.

## Relevant deferred constraints

- Profile or cheaply reject the initial path search for very large groups targeting a disconnected domain. CNC-19 bounds subsequent retries, but a deliberately hostile 150-unit cross-island order still has an expensive one-time search batch.
- Keep a repeatable Normal/Fastest performance baseline with at least five active AIs and, where feasible, 300 or more mobile units per AI. Record real-time/game-time ratio, actor counts and profiler hotspots before accepting optimization claims.

## Selection rationale

CNC-45 is the first eligible pending task in task-sheet order after the coordinator-excluded CNC-87, CNC-40, CNC-41, CNC-42, and CNC-44, and after tasks already active, complete, or first-iteration. It is a focused bug/polish AI behavior task with no stated prerequisite, user-question gate, or pinned-final restriction. CNC-26C is permanently pinned final and ineligible.
