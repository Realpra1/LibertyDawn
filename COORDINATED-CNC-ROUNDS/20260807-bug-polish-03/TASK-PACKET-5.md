# Task packet
- Task ID: CNC-52
- Title: Starting-Fact wall hole prevention/repair
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base, `468ee64f5a0f9a9e19e260e5c5943e6e878f4705` (`agent/cnc-20260807-bug-polish-02-release`). No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: CNC-45, CNC-46, CNC-47, and CNC-50 are claimed by slots 1–4 for this round. CNC-46 has direct wall-system overlap, including general wall self-blocking/selling behavior, which CNC-52 must leave to that task. CNC-40, CNC-41, CNC-42, CNC-44, and CNC-87 are excluded in-progress prior-round work.
- Cross-worker concern: Limit this work to the first deployed MCV/Fact and its first five game minutes. Coordinate wall interactions with CNC-46 without changing its active contract; monitor the other task's commits if they materially affect wall placement or repair ownership.

## Authoritative task text

**Starting-Fact wall hole prevention/repair.** For only the first deployed MCV/Fact and only during the first five game minutes, prevent avoidable friendly construction from consuming its planned wall cells or periodically repair incomplete wall-ring cells as well as map/build-radius constraints allow. Do not wall later Facts and stop maintenance after five minutes. Treat an occupied ring cell intelligently and repair it if it later becomes available. Preserve access and leave general wall self-blocking/selling behavior to CNC-46. The preserved manual log records Brutalis 2 abandoning its starting enclosure after eight blocked attempts.

## Relevant linked notes

- CNC-46 — Defense clusters: handles general wall self-blocking/selling behavior; this task must preserve that ownership boundary.
- CNC-41 — Economy Tiberium fields is active prior-round work that separately specifies red-tree/resonator wall behavior; it is excluded and must not be subsumed.

## Relevant deferred constraints

No CNC-52-specific deferred constraint was found in `DEFERRED_WORK.md`.

## Selection rationale

CNC-52 is the first eligible pending bug/polish task in sheet order after coordinator exclusions CNC-87, CNC-40, CNC-41, CNC-42, and CNC-44, and the same-round claims CNC-45, CNC-46, CNC-47, and CNC-50. CNC-48 is a cumulative integration task dependent on the tasks currently claimed in this round; CNC-49 explicitly requires the CNC-47 performance baseline and CNC-48 integration report, so neither is eligible for an isolated worker selection now. CNC-52 has no stated prerequisite, user-question gate, or pinned-final restriction. CNC-26C remains pinned final and ineligible.
