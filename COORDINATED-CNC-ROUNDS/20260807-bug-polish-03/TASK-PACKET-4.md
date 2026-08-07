# Task packet
- Task ID: CNC-50
- Title: Late-game engineer stall recovery
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base, `468ee64f5a0f9a9e19e260e5c5943e6e878f4705` (`agent/cnc-20260807-bug-polish-02-release`). No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: CNC-45, CNC-46, and CNC-47 are claimed by slots 1–3 for this round. CNC-39 is complete and CNC-39A is first iteration, with shared engineer/commando reservation behavior that CNC-50 must preserve. CNC-40, CNC-41, CNC-42, CNC-44, and CNC-87 are excluded in-progress prior-round work.
- Cross-worker concern: Coordinate with CNC-39/CNC-39A rather than duplicating or regressing their engineer-value, pairing, and reservation rules. Keep the recovery scope distinct from CNC-59's later neutral-building capture and specialist transport work.

## Authoritative task text

**Late-game engineer stall recovery.** Investigate engineers that remain idle despite nearby capturable vehicle husks or buildings, across every AI type and especially after owners/targets die in the late game. Revalidate assignments periodically and whenever an engineer has no valid order; release stale target reservations, rank newly available local husks/buildings normally, and recover rather than remaining stopped until death. Add bounded diagnostics that explain why visible candidates were excluded. Preserve and use the manual-game evidence in `AUTONOMOUS-CNC-LOGS/manual-post-cnc25-20260803-213129/`; it contains repeated late-game `no eligible solo target` stops. Coordinate with CNC-39/CNC-39A rather than duplicating or regressing their value, pairing, and reservation rules.

## Relevant linked notes

- CNC-39 — Engineer correction: lone engineers may capture enemy buildings below 80% health and existing reassessment, husk-value, coordinated capture, and pairing behavior remain in scope to preserve.
- CNC-39A — Engineer/commando target coordination: capture and demolition share deterministic reservations, and a queued C4 order revalidates ownership/relationship.
- CNC-59 — Dynamic neutral-building capture demand and specialist transport is later pending work; do not subsume its production-demand, neutral-target, or transport contract.

## Relevant deferred constraints

- Healthy-building engineer pairs previously stayed committed while the target remained healthy, which could suppress useful reassessment. The new engineer-correction task changes the capture threshold to 80% and should revalidate pair retargeting.

## Selection rationale

CNC-50 is the first eligible pending bug/polish task in sheet order after coordinator exclusions CNC-87, CNC-40, CNC-41, CNC-42, and CNC-44, and the same-round claims CNC-45, CNC-46, and CNC-47. CNC-48 is a cumulative integration task dependent on the tasks currently claimed in this round; CNC-49 explicitly requires the CNC-47 performance baseline and CNC-48 integration report, so neither is eligible for an isolated worker selection now. CNC-50 has no stated prerequisite, user-question gate, or pinned-final restriction. CNC-26C remains pinned final and ineligible.
