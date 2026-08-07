# Task packet
- Task ID: CNC-39
- Title: Engineer correction
- Status at selection: pending
- Required base/prerequisites: Use the coordinated round's recorded common base. No explicit prerequisite is stated in the task sheet.
- Related active tasks or PRs: CNC-39A (Engineer/commando target coordination) is pending and closely related; no active branch or PR matching CNC-39/engineer/commando was found at selection.
- Cross-worker concern: CNC-39A will share capture-target reservation and ownership semantics. Do not claim or redesign commando coordination beyond what is needed for CNC-39; monitor any later CNC-39A work for overlap.

## Authoritative task text

**Engineer correction.** A lone engineer may capture an enemy building below 80% health (rather than the old 50% threshold). Revalidate five-second/value-distance reassessment, valuable husks, coordinated healthy-building captures, and pairing without stale long routes.

## Relevant linked notes

- CNC-39A: **Engineer/commando target coordination.** Prevent engineers and commandos from entering or acting on the same building concurrently. Share deterministic target reservations between capture and demolition assignments, and revalidate ownership/relationship when a queued C4 order executes so a building captured in the meantime can never be detonated after becoming friendly. Test both simultaneous selection and capture-during-commando-travel races.

## Relevant deferred constraints

- Healthy-building engineer pairs previously stayed committed while the target remained healthy, which could suppress useful reassessment. The new engineer-correction task changes the capture threshold to 80% and should revalidate pair retargeting.

## Selection rationale

CNC-39 is the first eligible pending task in task-sheet order, is not excluded, has no stated prerequisite or user-question gate, and is a focused bug/polish correction. CNC-26C is permanently pinned final and ineligible; completed and first-iteration tasks are not selected.
